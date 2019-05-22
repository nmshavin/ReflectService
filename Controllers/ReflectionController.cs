using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using ReflectService.Hubs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace ReflectService.Controllers
{
    public class ReflectionController : Controller
    {
        public ReflectionController(IHubContext<ReflectServiceHub> hubContext)
        {
            _reflectServiceHub = hubContext;
        }

        private static IHubContext<ReflectServiceHub> _reflectServiceHub;
        private static readonly List<TimedAction> ActionList = new List<TimedAction>();

        [EnableCors("AllowAll")]
        [HttpPost]
        public async Task<JsonResult> GetTimedActions()
        {
            JObject output = new JObject();

            try
            {
                using (var streamReader = new HttpRequestStreamReader(Request.Body, Encoding.UTF8))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var json = await JObject.LoadAsync(jsonReader);
                    output.Add("actionList", JArray.FromObject(ActionList));
                }
            }
            catch (Exception ex) { output.Add("error", ex.Message); }

            return new JsonResult(output);
        }

        [EnableCors("AllowAll")]
        [HttpPost]
        public async Task<JsonResult> AddTimedAction()
        {
            var output = new JObject();
            using (var streamReader = new HttpRequestStreamReader(Request.Body, Encoding.UTF8))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var jsonRoot = await JObject.LoadAsync(jsonReader);
                var sessionGuid = jsonRoot["sessionGuid"];
                string uniqueId;
                if (null == sessionGuid)
                {
                    uniqueId = Guid.NewGuid().ToString();
                }
                else
                {
                    uniqueId = (string)((JValue)sessionGuid).Value;
                }

                HttpContext.Session.Set("sessionGuid", Encoding.ASCII.GetBytes(uniqueId));
                output.Add("sessionGuid", uniqueId);

                var methods = jsonRoot["methods"];
                var methodsArrayOutput = new JArray();
                foreach (var methodNode in methods.ToList())
                {
                    var retVal = Json("{ retVal: 'Error!' }");
                    var methodName = methodNode["name"].ToString();
                    string callbackMethodName = null;
                    var callbackMethod = methodNode["callbackMethod"];
                    var intervalTimerToken = methodNode["intervalTimer"];
                    var interval = int.Parse(((JValue)intervalTimerToken).Value.ToString());
                    if (null != callbackMethod)
                    {
                        callbackMethodName = callbackMethod.ToString();
                    }

                    try
                    {
                        var typeOk = GetReflectedTypeFromJsonToken(methodNode, out var reflectedType);

                        if (typeOk)
                        {
                            var methodInfo = reflectedType.GetMethod(methodName);
                            if (null != methodInfo)
                            {
                                var parametersInfo = methodInfo.GetParameters();
                                var parametersToken = methodNode["parameters"];
                                var parameters = ExtractParameters(parametersInfo, parametersToken, uniqueId);
                                var timedAction = new TimedAction(_reflectServiceHub, methodInfo, ref parameters, interval, callbackMethodName, uniqueId);
                                ActionList.Add(timedAction);
                                output.Add("timedActionId", timedAction.ActionID);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        output.Add("error", ex.Message);
                    }
                }
            }
            
            return Json(output);
        }

        [EnableCors("AllowAll")]
        [HttpPost]
        public async Task<JsonResult> RemoveTimedAction()
        {
            var output = new JObject();

            using (var streamReader = new HttpRequestStreamReader(Request.Body, Encoding.UTF8))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var jsonRoot = await JObject.LoadAsync(jsonReader);
                var actionIdToken = jsonRoot["actionID"];
                var removeStatus = false;
                if (null != actionIdToken)
                {
                    Guid.TryParse((string)((JValue)actionIdToken).Value, out Guid actionID);
                    List<TimedAction> removableActions = ActionList.Where(a => a.ActionID.Equals(actionID)).ToList();
                    removableActions.ForEach(action => { action.Dispose(); ActionList.Remove(action); });
                    removeStatus = true;

                }

                output.Add("Removed", removeStatus);
            }
            return Json(output);

        }

        [EnableCors("AllowAll")]
        [HttpPost]
        public async Task<JsonResult> GetMethodList()
        {
            var output = new JObject();

            using (var streamReader = new HttpRequestStreamReader(Request.Body, Encoding.UTF8))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var jsonRoot = await JObject.LoadAsync(jsonReader);
                var sessionGuid = jsonRoot["sessionGuid"];
                string uniqueId;
                if (null == sessionGuid)
                {
                    uniqueId = Guid.NewGuid().ToString();
                }
                else
                {
                    uniqueId = (string)((JValue)sessionGuid).Value;
                }

                HttpContext.Session.Set("sessionGuid", Encoding.ASCII.GetBytes(uniqueId));
                output.Add("sessionGuid", uniqueId);
                var methodInfoArray = new List<object>();

                try
                {
                    var typeOk = GetReflectedTypeFromJsonToken(jsonRoot, out Type reflectedType);

                    if (typeOk)
                    {
                        methodInfoArray.AddRange(reflectedType.GetMethods().Select(methodInfo => new Dictionary<string, object>
                        { {"name", methodInfo.Name}, {"retVal", methodInfo.ReturnType.ToString()}, {"parameters", methodInfo.GetParameters().Select(param => param.ToString())}}
                        ).Cast<object>());
                    }
                }
                catch (Exception ex)
                {
                    output.Add("error", ex.Message);
                }

                output.Add("reflectedAssembly", jsonRoot["reflectedAssembly"]);
                output.Add("reflectedType", jsonRoot["reflectedType"]);

                output.Add("methodInfoArray", JArray.FromObject(methodInfoArray));
            }

            return Json(output);
        }

        [EnableCors("AllowAll")]
        [HttpPost]
        public async Task<JsonResult> GetMethodInfo()
        {
            var output = new Dictionary<string, object>();
            using (var streamReader = new HttpRequestStreamReader(Request.Body, Encoding.UTF8))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var jsonRoot = await JObject.LoadAsync(jsonReader);
                var methodName = jsonRoot["method"].ToString();

                try
                {
                    var typeOk = GetReflectedTypeFromJsonToken(jsonRoot, out var reflectedType);

                    if (typeOk)
                    {
                        MethodInfo methodInfo = reflectedType.GetMethod(methodName);

                        output.Add("name", methodInfo.Name);
                        output.Add("retVal", methodInfo.ReturnType.ToString());
                        output.Add("parameters", methodInfo.GetParameters().Select(param => param.ToString()));
                    }
                }
                catch (Exception ex)
                {
                    output.Add("error", ex.Message);
                }
            }
            return Json(output);
        }

        [HandleProcessCorruptedStateExceptions]
        [EnableCors("AllowAll")]
        [HttpPost]
        public async Task<JsonResult> CallMethod()
        {
            var output = new Dictionary<string, object>();
            using (var streamReader = new HttpRequestStreamReader(Request.Body, Encoding.UTF8))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var jsonRoot = await JObject.LoadAsync(jsonReader);
                var sessionGuid = jsonRoot["sessionGuid"];
                string uniqueId;
                if (null == sessionGuid)
                {
                    uniqueId = Guid.NewGuid().ToString();
                }
                else
                {
                    uniqueId = (string)((JValue)sessionGuid).Value;
                }

                HttpContext.Session.Set("sessionGuid", Encoding.ASCII.GetBytes(uniqueId));
                output.Add("sessionGuid", uniqueId);

                var methods = jsonRoot["methods"];
                var methodsArrayOutput = new JArray();
                foreach (var methodNode in methods.ToList())
                {
                    var retVal = Json("{ retVal: 'Error!' }");
                    var methodName = methodNode["name"].ToString();
                    string callbackMethodName = null;
                    var callbackMethod = methodNode["callbackMethod"];

                    if (null != callbackMethod)
                    {
                        callbackMethodName = callbackMethod.ToString();
                    }

                    var methodOutput = new JObject
                {
                    { "name", methodName },
                    { "callbackMethod", callbackMethodName }
                };

                    try
                    {
                        var typeOk = GetReflectedTypeFromJsonToken(methodNode, out Type reflectedType);

                        if (typeOk)
                        {
                            //MethodInfo methodInfo = reflectedType.GetMethod(methodName);
                            var methodInfo = reflectedType.GetMethods().First(method => method.Name.Equals(methodName) && method.GetParameters().Length == methodNode["parameters"].Count());
                            if (null != methodInfo)
                            {
                                var parametersInfo = methodInfo.GetParameters();
                                var parametersToken = methodNode["parameters"];
                                var parameters = ExtractParameters(parametersInfo, parametersToken, uniqueId);
                                try
                                {
                                    var retValObj = methodInfo.Invoke(null, parameters) ?? "null";
                                    methodOutput.Add("retVal", JToken.FromObject(retValObj));

                                    var paramsList = new Dictionary<string, object>();
                                    if (parametersInfo.Length > 0)
                                    {
                                        var paramsObject = new JObject();
                                        for (uint i = 0; i < parametersInfo.Length; ++i)
                                        {
                                            var paramInfo = parametersInfo[i];
                                            var elementType = parameters[i].GetType();
                                            if (elementType.IsSubclassOf(typeof(Delegate)))
                                            {
                                                paramsObject.Add(paramInfo.Name, JToken.FromObject(parametersToken[paramInfo.Name].ToString()));
                                            }
                                            else
                                            {
                                                paramsObject.Add(paramInfo.Name, JToken.FromObject(parameters[i]));
                                            }
                                        }
                                        methodOutput.Add("parameters", paramsObject);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    methodOutput.Add("error", ex.Message);
                                }

                                methodsArrayOutput.Add(methodOutput);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        output.Add("error", ex.Message);
                    }
                }
                output.Add("methods", methodsArrayOutput);
            }

            return Json(output);
        }

        public static void ForwardCallback(params object[] args)
        {
            var i = 0;
            var sessionGuid = (string)args[i++];
            var methodOutput = JObject.Parse((string)args[i++]);
            methodOutput.Add("sessionGuid", sessionGuid);
            var methodDescription = methodOutput["methodDescription"];
            var parameters = methodDescription["parameters"];
            var parameter = parameters.First;
            if (null != parameter)
            {
                for (; parameter != parameters.Last.Next; parameter = parameter.Next, ++i)
                {
                    var property = parameter.Value<JProperty>();
                    if (args[i] is IntPtr)
                    {
                        var data = Marshal.ReadInt64((IntPtr)args[i]);
                        property.Value = JToken.FromObject(data.ToString("X"));
                    }
                    else
                    {
                        property.Value = JToken.FromObject(args[i]);
                    }

                }
            }
            _reflectServiceHub.Clients.All.SendAsync("ReceiveCallback", methodOutput);
        }

        private static bool GetReflectedTypeFromJsonToken(JToken jsonToken, out Type reflectedType)
        {
            var bRetVal = false;
            reflectedType = null;
            var reflectedAssemblyToken = jsonToken["reflectedAssembly"];
            if (null == reflectedAssemblyToken) return false;

            var reflectedAssemblyName = (string)((JValue)reflectedAssemblyToken).Value;
            try
            {
                var reflectedAssembly = Assembly.LoadFrom(reflectedAssemblyName);
                if (null != reflectedAssembly)
                {
                    var reflectedTypeToken = jsonToken["reflectedType"];
                    reflectedType = reflectedAssembly.GetType((string)((JValue)reflectedTypeToken).Value);
                    bRetVal = reflectedType != null;
                }
            }
            catch
            {
                throw new Exception("Error Loading type from assembly");
            }

            return bRetVal;
        }

        private static Object[] ExtractParameters(ParameterInfo[] parametersInfo, JToken parametersToken, string uniqueId)
        {
            var parameters = new Object[parametersInfo.Length];

            for (uint i = 0; i < parametersInfo.Length; ++i)
            {
                var paramInfo = parametersInfo[i];
                var paramToken = parametersToken[paramInfo.Name];

                var deserialized = DeserializeParameter(paramToken, paramInfo, uniqueId);


                parameters[i] = deserialized;
            }

            return parameters;
        }

        private static object DeserializeParameter(JToken paramToken, ParameterInfo paramInfo, string uniqueId)
        {
            object deserialized = null;
            var elementType = paramInfo.ParameterType;

            if (elementType.IsByRef)
            {
                var referencedElementType = elementType.GetElementType();

                var referencedValueList = new List<object>();
                if (null != ((JValue)paramToken).Value)
                {
                    referencedValueList.Add(GetElementValue(paramToken, uniqueId, referencedElementType));

                    if (referencedElementType.IsPrimitive)
                    {
                        deserialized = GetElementValue(paramToken, uniqueId, referencedElementType);
                    }
                }

                if (null == deserialized)
                {
                    deserialized = Activator.CreateInstance(referencedElementType, referencedValueList.ToArray());
                }
            }
            else
            {
                try
                {
                    deserialized = GetElementValue(paramToken, uniqueId, elementType);
                }
                catch (Exception)
                {
                    deserialized = Activator.CreateInstance(elementType);
                }
            }

            return deserialized;
        }

        private static object GetElementValue(JToken paramToken, string uniqueId, Type elementType)
        {
            object deserialized;
            if (elementType.BaseType == typeof(Enum))
            {
                deserialized = Enum.Parse(elementType, paramToken.ToString());
            }
            else if (elementType.IsSubclassOf(typeof(Delegate)))
            {
                deserialized = DelegateGenerator.CreateDelegateClientCallback(typeof(ReflectionController).GetMethod("ForwardCallback"), uniqueId, elementType, paramToken.ToString());
            }
            else
            {
                deserialized = paramToken.ToObject(elementType);
            }

            return deserialized;
        }
    }
}
