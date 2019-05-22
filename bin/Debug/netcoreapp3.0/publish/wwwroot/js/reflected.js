class ReflectService {
	constructor(serverIPAddress, serverPort) {
		this.serverIPAddress = serverIPAddress;
		this.serverPort = serverPort;
        window.fcnToName = new Map();
        window.nameToFcn = new Map();

        this.registerReflectServiceHub();

		this.utils = {
                serverIPAddress: this.serverIPAddress,
                serverPort: this.serverPort,

			callMethods(methods) {
				if (methods && methods.length > 0) {
					return this.postData('http://' + this.serverIPAddress + ':' + this.serverPort + '/Reflection/CallMethod', { sessionGuid: sessionStorage.sessionGuid, methods: methods });
				}
			},

			callMethod(method) {
				if (method) {
					return this.postData('http://' + this.serverIPAddress + ':' + this.serverPort + '/Reflection/CallMethod', { sessionGuid: sessionStorage.sessionGuid, methods: [method] });
				}
			},

			addTimedAction(method) {
				if (method) {
					return this.postData('http://' + this.serverIPAddress + ':' + this.serverPort + '/Reflection/AddTimedAction', { sessionGuid: sessionStorage.sessionGuid, methods: [method] });
				}
			},

			getTimedActions() {
				return this.postData('http://' + this.serverIPAddress + ':' + this.serverPort + '/Reflection/GetTimedActions', {});
			},

			removeTimedAction(actionID) {
				return this.postData('http://' + this.serverIPAddress + ':' + this.serverPort + '/Reflection/RemoveTimedAction', { actionID: actionID });
			},

			postData(url, data) {
				// Default options are marked with *
				return fetch(url, {
					body: JSON.stringify(data), // must match 'Content-Type' header
					cache: 'no-cache', // *default, no-cache, reload, force-cache, only-if-cached
					credentials: 'same-origin', // include, same-origin, *omit
					headers: {
						'user-agent': 'Mozilla/4.0 MDN Example',
						'content-type': 'application/json'
					},
					method: 'POST', // *GET, POST, PUT, DELETE, etc.
					mode: 'cors', // no-cors, cors, *same-origin
					redirect: 'follow', // manual, *follow, error
					referrer: 'client', // *client, no-referrer
				})
					.then(response => {
						return response.json();
					}) // parses response to JSON
            },

            Create_UUID() {
                var dt = new Date().getTime();
                var uuid = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
                    var r = (dt + Math.random() * 16) % 16 | 0;
                    dt = Math.floor(dt / 16);
                    return (c == 'x' ? r : (r & 0x3 | 0x8)).toString(16);
                });
                return uuid;
            }
		}
	}

	async reflectType(reflectedAssembly, reflectedType) {
		return this.utils.postData('http://' + this.serverIPAddress + ':' + this.serverPort + '/Reflection/GetMethodList', { reflectedAssembly: reflectedAssembly, reflectedType: reflectedType })
			.then(response => {
				clearInterval();
				return this.handleMethodList(response);
			});
	}

	handleMethodList(a) {
		let reflectionContainer = {
			utils: this.utils
		}

		if (!sessionStorage.sessionGuid) {
			sessionStorage.sessionGuid = a.sessionGuid;
		}

		for (let i = 0; i < a.methodInfoArray.length; ++i) {
			let method = a.methodInfoArray[i],
				methodText = `function (`,
				parametersText = [];

			if (method.parameters) {
				for (let j = 0; j < method.parameters.length; ++j, methodText += ',') {
					let parameter = method.parameters[j],
						paramName = parameter.split(" ").pop();

					parametersText[j] = paramName;
					methodText += `${paramName}`;
				}
			}

            methodText += `time, callbackMethod) {
    var UUID = window.fcnToName.get(callbackMethod);
    if(undefined == UUID) {
        UUID = this.utils.Create_UUID();
        window.fcnToName.set(callbackMethod, UUID);
        window.nameToFcn.set(UUID, callbackMethod);
    }

	let callbackHandler = callbackMethod == undefined ? "" : UUID;

	let method = { reflectedAssembly: '` + a.reflectedAssembly + `', reflectedType: '` + a.reflectedType + `', name: '` + method.name + `', parameters: { `;
			if (method.parameters) {
				for (let j = 0; j < method.parameters.length; ++j) {
					let paramText = parametersText[j];
					methodText += paramText + ': ' + paramText;

					if (j + 1 < parametersText.length) {
						methodText += ', ';
					}
				}
			}

			methodText += `}, callbackMethod: callbackHandler, reflectedAssembly: a.reflectedAssembly, reflectedType: a.reflectedType };
		if(undefined == time)
		{
			return this.utils.callMethod(method);
		}
		else
		{
			method.intervalTimer = time;
			return this.utils.addTimedAction(method);
		}
	}`;

			eval(`reflectionContainer.${method.name} = ` + methodText);
		}

		return reflectionContainer;
	}

	handleCallback(a) {
		if (a.callbackMethod) {
			let method;
			if (a.callbackMethod.charAt(0) == '@') {
				method = this.anonymousFuncBin[a.callbackMethod];
				delete this.anonymousFuncBin[a.callbackMethod];
			}
			else {
				method = window[a.callbackMethod];
			}

			if (method) {
				method(a);
			}
		}
	}

	handleCallMethod(a, b) {
		a.methods.forEach(handleCallback);
    }

    registerReflectServiceHub() {
        "use strict";

        var connection = new signalR.HubConnectionBuilder().withUrl('http://' + this.serverIPAddress + ':' + this.serverPort +'/reflectServiceHub').build();

        connection.on("ReceiveCallback", function (callbackData) {
            if (callbackData) {
                if (callbackData.sessionGuid === sessionStorage.sessionGuid) {
                    let method = window[callbackData.callbackMethod];
                    if (method) {
                        method(callbackData.methodDescription);
                    }
                }
            }
        });

        connection.on("ReceiveTimedActionResult", function (callbackData) {
            if (callbackData) {
                if (callbackData.sessionGuid === sessionStorage.sessionGuid) {
                    let method = window.nameToFcn.get(callbackData.callbackMethod);
                    if (method) {
                        method(callbackData);
                    }
                }
            }
        });

        connection.onclose(a => connection.start());

        connection.start().catch(function (err) {
            return console.error(err.toString());
        });
    }
}