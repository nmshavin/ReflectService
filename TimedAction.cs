using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using ReflectService.Hubs;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace ReflectService
{
    public class TimedAction : IDisposable
    {
        private const string Null = "null";

        public TimedAction(IHubContext<ReflectServiceHub> reflectServiceHub, MethodInfo methodInfo, ref object[] methodParameters, int interval, string callbackMethodName, string sessionGuid)
        {
            this.Callback = callbackMethodName;
            this._methodInfo = methodInfo;
            this._methodParameters = methodParameters;
            this._interval = interval;
            this.ActionID = Guid.NewGuid();

            _timer = new Timer(new TimerCallback(parametersData =>
            {
                if (!(parametersData is object[] origParametersData)) return;
                var parametersInstance = origParametersData.Clone() as object[];
                var methodOutput = new JObject();

                var retValObj = methodInfo.Invoke(null, parametersInstance) ?? Null;

                methodOutput.Add("name", methodInfo.Name);
                methodOutput.Add("callbackMethod", callbackMethodName);
                methodOutput.Add("retVal", JToken.FromObject(retValObj));

                var parametersInfo = methodInfo.GetParameters();
                var paramsList = new Dictionary<string, object>();
                if (parametersInfo.Length > 0)
                {
                    var paramsObject = new JObject();
                    for (uint i = 0; i < parametersInfo.Length; ++i)
                    {
                        var paramInfo = parametersInfo[i];
                        var elementType = parametersInstance[i].GetType();
                        if (elementType.IsSubclassOf(typeof(Delegate)))
                        {
                            throw new Exception("not impl.");
                        }
                        else
                        {
                            paramsObject.Add(paramInfo.Name, JToken.FromObject(parametersInstance[i]));
                        }
                    }
                    methodOutput.Add("parameters", paramsObject);
                }
                methodOutput.Add("timedActionId", ActionID);
                methodOutput.Add("sessionGuid", sessionGuid);
                reflectServiceHub.Clients.All.SendAsync("ReceiveTimedActionResult", methodOutput);
            }),
            methodParameters, 0, interval);
        }

        private readonly MethodInfo _methodInfo;
        private readonly object[] _methodParameters;
        private readonly int _interval;
        private readonly Timer _timer;

        public int Interval { get => _interval; }
        public object[] MethodParameters { get => _methodParameters; }
        public string MethodName { get => _methodInfo.Name; }
        public string Callback { get; }
        public Guid ActionID { get; }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
