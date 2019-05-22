using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using ReflectService.Hubs;

namespace ReflectService
{
    public class DelegateGenerator
    {
        public static Delegate CreateDelegateClientCallback(MethodInfo fwdMethodInfo, string sessionGuid, Type delegateType, string clientCallbackMethod)
        {
            var methodInfo = ((MethodInfo[])((System.Reflection.TypeInfo)delegateType).DeclaredMethods)[0];
            var parameterTypes = methodInfo.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            //MethodInfo fwdMethodInfo = typeof(ReflectionController).GetMethod("ForwardCallback");
            var dynamicMethod = new DynamicMethod(methodInfo.Name, methodInfo.ReturnType.GetElementType(), parameterTypes);
            var il = dynamicMethod.GetILGenerator();
            var args = il.DeclareLocal(typeof(object[]));
            var type = il.DeclareLocal(typeof(Type));

            var outputArrayLength = parameterTypes.Length + 2; //objects array will contain the guid in the first cell and the method description in the second cell

            // create array
            il.Emit(OpCodes.Ldc_I4_S, outputArrayLength);
            il.Emit(OpCodes.Newarr, typeof(object));
            il.Emit(OpCodes.Stloc, args);
            il.Emit(OpCodes.Ldloc, args);

            il.Emit(OpCodes.Ldc_I4_S, 0);
            il.Emit(OpCodes.Ldstr, sessionGuid);
            il.Emit(OpCodes.Stelem_Ref);
            il.Emit(OpCodes.Ldloc, args);

            // insert callback method name, return type and parameters (as JSON) to array
            var methodInfoOutput = new Dictionary<string, object>
            {
                // method to be called on client side once event is received
                ["callbackMethod"] = clientCallbackMethod
            };

            var methodDescription = new Dictionary<string, object>
            {
                { "Name", methodInfo.DeclaringType.Name },
                { "retVal", methodInfo.ReturnType.ToString() }
            };
            var parameters = methodInfo.GetParameters().ToDictionary<ParameterInfo, string, object>(parameter => parameter.Name, parameter => "");

            methodDescription.Add("parameters", parameters);
            methodInfoOutput["methodDescription"] = methodDescription;

            il.Emit(OpCodes.Ldc_I4_S, 1);
            il.Emit(OpCodes.Ldstr, JObject.FromObject(methodInfoOutput).ToString());
            il.Emit(OpCodes.Stelem_Ref);
            il.Emit(OpCodes.Ldloc, args);

            // insert method parameters to array
            if (parameterTypes.Length > 0)
            {
                for (var index = 0; index < parameterTypes.Length; index++)
                {
                    il.Emit(OpCodes.Ldc_I4_S, index + 2);
                    il.Emit(OpCodes.Ldarg, index);

                    Type parameterType = parameterTypes[index];

                    if (parameterType.IsValueType || parameterType.IsGenericParameter)
                    {
                        il.Emit(OpCodes.Box, parameterType);
                    }

                    il.Emit(OpCodes.Stelem_Ref);
                    il.Emit(OpCodes.Ldloc, args);
                }
            }
            
            il.Emit(OpCodes.Call, fwdMethodInfo);


            #region Remarked: More Felixable Code, but requires a higher level of MSIL to maintain
            /* if (methodInfo.ReturnType != typeof(void))
             {
                 // if not generic param to do normal processing , for generic use special
                 //ubox_any
                 if (!methodInfo.ReturnType.IsGenericParameter)
                 {
                     if (methodInfo.ReturnType.IsPrimitive || methodInfo.ReturnType.IsValueType)
                     {
                         il.Emit(OpCodes.Unbox, methodInfo.ReturnType);
                         il.Emit(OpCodes.Ldobj, methodInfo.ReturnType);
                     }
                     else if (methodInfo.ReturnType.IsValueType)
                     {
                         il.Emit(OpCodes.Unbox, methodInfo.ReturnType);
                     }
                 }
                 else
                 {
                     il.Emit(OpCodes.Unbox_Any, methodInfo.ReturnType);
                 }
             }*/
            #endregion

            il.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate(delegateType);
        }
    }
}
