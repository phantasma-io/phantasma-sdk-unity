using System;
using System.Collections;
using UnityEngine.Networking;
using System.Text;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using PhantasmaPhoenix.Unity.Core.Logging;

[assembly: InternalsVisibleTo("PhantasmaPhoenix.Unity.Core.Tests")]

namespace PhantasmaPhoenix.Unity.Core
{
    public static class WebClient
    {
        public const int DefaultTimeout = 0;
        public const int DefaultRetries = 0;
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new DefaultNamingStrategy() },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = { new StringEnumConverter(new DefaultNamingStrategy(), allowIntegerValues: true) }
        };
        private static readonly JsonSerializer NewtonsoftJsonSerializer = JsonSerializer.Create(JsonSerializerSettings);
        private static long requestNumber = 0;
        private static object requestNumberLock = new object();
        private static long GetNextRequestNumber()
        {
            lock (requestNumberLock)
            {
                if (requestNumber == Int64.MaxValue)
                    requestNumber = 0;
                else
                    requestNumber++;
            }

            return requestNumber;
        }

        public class JsonRpcRequest
        {
            public string jsonrpc = "2.0";
            public string method;
            public string id;
            public object[] @params;

            public JsonRpcRequest(string method, object[] parameters, string id)
            {
                this.method = method;
                this.@params = parameters;
                this.id = id;
            }
        }
        public class JsonRpcError
        {
            public int code;
            public string message;
        }
        public class JsonRpcResponse<T>
        {
            public string jsonrpc;
            public string id;
            public T result;
            public JsonRpcError error;
        }
        
        private static string FormatError(UnityWebRequest request, string url, JsonRpcError parsedError = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(request.error ?? "Unknown error");
            sb.AppendLine($"URL: {url}");
            sb.AppendLine($"Is connection error: {request.result == UnityWebRequest.Result.ConnectionError}");
            sb.AppendLine($"Is protocol error: {request.result == UnityWebRequest.Result.ProtocolError}");
            sb.AppendLine($"Is data processing error: {request.result == UnityWebRequest.Result.DataProcessingError}");
            sb.AppendLine($"Response code: {request.responseCode}");

            if (parsedError != null)
            {
                sb.AppendLine($"Error code: {parsedError.code}");
                sb.AppendLine($"Error message: {parsedError.message}");
            }

            return sb.ToString();
        }

        private static JsonRpcError TryParseJsonRpcError(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<JsonRpcResponse<object>>(json, JsonSerializerSettings)?.error;
            }
            catch { return null; }
        }

        private static T ParseResponse<T>(string response)
        {
            if (typeof(T) == typeof(string))
            {
                return (T)(object)response;
            }

            return JsonConvert.DeserializeObject<T>(response, JsonSerializerSettings);
        }

        internal static bool TryDecodeRpcResponse<T>(string response, string expectedRequestId, out T result, out EPHANTASMA_SDK_ERROR_TYPE errorType, out string errorMessage)
        {
            result = default;
            errorType = EPHANTASMA_SDK_ERROR_TYPE.MALFORMED_RESPONSE;
            errorMessage = null;

            JObject envelope;
            try
            {
                envelope = JObject.Parse(response);
            }
            catch (Exception e)
            {
                errorType = EPHANTASMA_SDK_ERROR_TYPE.FAILED_PARSING_JSON;
                errorMessage = "Failed to parse RPC response: \"" + e.Message + "\"";
                return false;
            }

            var idToken = envelope["id"];
            if (idToken == null || idToken.Type == JTokenType.Null)
            {
                errorMessage = $"Missing response id for request {expectedRequestId}";
                return false;
            }

            if (idToken.Type != JTokenType.String && idToken.Type != JTokenType.Integer)
            {
                errorMessage = $"JSON-RPC id must be a string, integer, or null, got {idToken.Type}";
                return false;
            }

            if (idToken.Type != JTokenType.String || idToken.Value<string>() != expectedRequestId)
            {
                errorMessage = $"Response id mismatch: got {idToken.ToString(Formatting.None)}, expected {expectedRequestId}";
                return false;
            }

            var errorToken = envelope["error"];
            if (errorToken != null && errorToken.Type != JTokenType.Null)
            {
                var rpcError = errorToken.ToObject<JsonRpcError>(NewtonsoftJsonSerializer);
                errorType = EPHANTASMA_SDK_ERROR_TYPE.API_ERROR;
                errorMessage = rpcError?.message ?? "RPC error";
                return false;
            }

            if (!envelope.ContainsKey("result"))
            {
                errorMessage = "Missing response result";
                return false;
            }

            var resultToken = envelope["result"];
            if (resultToken == null || resultToken.Type == JTokenType.Null)
            {
                return true;
            }

            try
            {
                result = resultToken.ToObject<T>(NewtonsoftJsonSerializer);
                return true;
            }
            catch (Exception e)
            {
                errorType = EPHANTASMA_SDK_ERROR_TYPE.FAILED_PARSING_JSON;
                errorMessage = "Failed to parse RPC response: \"" + e.Message + "\"";
                return false;
            }
        }

        public static IEnumerator RPCRequest<T>(string url, string apiKey, string method, int timeout, int retriesOnNetworkError, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback,
                                            Action<T> callback, params object[] parameters)
        {
            var requestNumber = GetNextRequestNumber().ToString();
            var rpcRequest = new JsonRpcRequest(method, parameters, requestNumber);
            var json = JsonConvert.SerializeObject(rpcRequest, JsonSerializerSettings);
            var paramCount = parameters?.Length ?? 0;

            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            Log.Write($"RPC request [{requestNumber}]\nurl: {url}\nmethod: {method}\nparams: {paramCount}\nbodyBytes: {bodyRaw.Length}", Log.Level.Networking);
            Log.Write($"RPC request [{requestNumber}]\nurl: {url}\njson: {json}", Log.Level.Debug1);

            DateTime startTime = DateTime.Now;

            UnityWebRequest.Result? result = null;
            string response = null;
            string formattedError = null;
            long responseCode = 0;

            for (; ; )
            {
                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(apiKey))
                    request.SetRequestHeader("X-Api-Key", apiKey);
                if (timeout > 0)
                    request.timeout = timeout;

                yield return request.SendWebRequest();
                result = request.result;
                responseCode = request.responseCode;
                response = request.downloadHandler.text;
                if (result == UnityWebRequest.Result.ConnectionError || result == UnityWebRequest.Result.ProtocolError || result == UnityWebRequest.Result.DataProcessingError)
                {
                    var parsedRpcError = TryParseJsonRpcError(response);
                    formattedError = FormatError(request, url, parsedRpcError);
                }

                if (result == UnityWebRequest.Result.Success || retriesOnNetworkError == 0)
                {
                    // success
                    break;
                }

                Log.Write($"RPC network error [{requestNumber}], {retriesOnNetworkError} retries left.", Log.Level.Networking);
                yield return new WaitForSeconds(1f);
                retriesOnNetworkError--;
            }

            TimeSpan responseTime = DateTime.Now - startTime;

            if (result == UnityWebRequest.Result.ConnectionError || result == UnityWebRequest.Result.ProtocolError || result == UnityWebRequest.Result.DataProcessingError)
            {
                Log.Write($"RPC error [{requestNumber}]\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{formattedError}", Log.Level.Networking);
                errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR, formattedError);
            }
            else
            {
                var responseBytes = string.IsNullOrEmpty(response) ? 0 : Encoding.UTF8.GetByteCount(response);
                Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nstatus: {responseCode}\nelapsedMs: {(long)responseTime.TotalMilliseconds}\nbodyBytes: {responseBytes}", Log.Level.Networking);
                Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{response}", Log.Level.Debug1);

                try
                {
                    if (TryDecodeRpcResponse<T>(response, requestNumber, out var decodedResult, out var errorType, out var errorMessage))
                    {
                        callback?.Invoke(decodedResult);
                    }
                    else
                    {
                        Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nInvalid JSON-RPC response: {errorMessage}", Log.Level.Networking);
                        errorHandlingCallback?.Invoke(errorType, errorMessage);
                    }
                }
                catch (Exception e)
                {
                    Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nFailed to parse JSON: " + e.ToString(), Log.Level.Networking);
                    errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.FAILED_PARSING_JSON, "Failed to parse RPC response: \"" + e.Message + "\"");
                    yield break;
                }
            }

            yield break;
        }

        public static IEnumerator RESTGet<T>(string url, int timeout, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, Action<T> callback)
        {
            var requestNumber = GetNextRequestNumber();
            Log.Write($"REST request [{requestNumber}]\nurl: {url}", Log.Level.Networking);

            using var request = new UnityWebRequest(url, "GET");
            request.downloadHandler = new DownloadHandlerBuffer();

            DateTime startTime = DateTime.Now;

            if (timeout > 0)
                request.timeout = timeout;
            
            yield return request.SendWebRequest();
            
            TimeSpan responseTime = DateTime.Now - startTime;

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                var error = FormatError(request, url);
                Log.Write($"REST error [{requestNumber}]\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{error}", Log.Level.Networking);
                errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR, error);
            }
            else
            {
                T response = default;
                try
                {
                    var responseText = request.downloadHandler.text;
                    var responseBytes = string.IsNullOrEmpty(responseText) ? 0 : Encoding.UTF8.GetByteCount(responseText);
                    Log.Write($"REST response [{requestNumber}]\nurl: {url}\nstatus: {request.responseCode}\nelapsedMs: {(long)responseTime.TotalMilliseconds}\nbodyBytes: {responseBytes}", Log.Level.Networking);
                    Log.Write($"REST response [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{responseText}", Log.Level.Debug1);
                    response = ParseResponse<T>(responseText);
                }
                catch(Exception e)
                {
                    Log.Write(e.Message);
                }
                callback(response);
            }

            yield break;
        }

        public static IEnumerator RESTPost<T>(string url, string serializedJson, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, Action<T> callback)
        {
            var requestNumber = GetNextRequestNumber();
            Log.Write($"REST request (POST) [{requestNumber}]\nurl: {url}\nbodyBytes: {Encoding.UTF8.GetByteCount(serializedJson)}", Log.Level.Networking);

            Log.Write($"REST request (POST) [{requestNumber}]\nserializedJson: {serializedJson}", Log.Level.Debug1);

            using var request = new UnityWebRequest(url, "POST");

            byte[] data = Encoding.UTF8.GetBytes(serializedJson);
            request.uploadHandler = new UploadHandlerRaw(data);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            DateTime startTime = DateTime.Now;
            yield return request.SendWebRequest();
            TimeSpan responseTime = DateTime.Now - startTime;

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                var error = FormatError(request, url);
                Log.Write($"REST error [{requestNumber}]\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{error}", Log.Level.Networking);
                errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR, error);
            }
            else
            {
                var responseText = request.downloadHandler.text;
                var responseBytes = string.IsNullOrEmpty(responseText) ? 0 : Encoding.UTF8.GetByteCount(responseText);
                Log.Write($"REST response [{requestNumber}]\nurl: {url}\nstatus: {request.responseCode}\nelapsedMs: {(long)responseTime.TotalMilliseconds}\nbodyBytes: {responseBytes}", Log.Level.Networking);
                Log.Write($"REST response [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{responseText}", Log.Level.Debug1);

                T response = default;
                try
                {
                    response = ParseResponse<T>(responseText);
                }
                catch(Exception e)
                {
                    Log.Write(e.Message);
                }
                callback(response);
            }

            yield break;
        }

        public static IEnumerator Ping(string url, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, Action<TimeSpan> callback)
        {
            var requestNumber = GetNextRequestNumber();
            Log.Write($"Ping url [{requestNumber}]: {url}", Log.Level.Networking);

            using var request = new UnityWebRequest(url, "GET");
            request.downloadHandler = new DownloadHandlerBuffer();

            DateTime startTime = DateTime.Now;
            yield return request.SendWebRequest();
            TimeSpan responseTime = DateTime.Now - startTime;

            // TODO return proper check later when PHA RPC would return something instead of 405 error code.
            // if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError || request.result == UnityWebRequest.Result.DataProcessingError)
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                var error = FormatError(request, url);
                Log.Write($"Ping error error [{requestNumber}]\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{error}", Log.Level.Networking);
                errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR, error);
            }
            else
            {
                var responseText = request.downloadHandler.text;
                var responseBytes = string.IsNullOrEmpty(responseText) ? 0 : Encoding.UTF8.GetByteCount(responseText);
                Log.Write($"Ping response [{requestNumber}]\nurl: {url}\nelapsedMs: {(long)responseTime.TotalMilliseconds}\nbodyBytes: {responseBytes}", Log.Level.Networking);
                Log.Write($"Ping response [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{responseText}", Log.Level.Debug1);
                callback(responseTime);
            }

            yield break;
        }
    }
}
