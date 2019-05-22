let reflectedService = new ReflectService("127.0.0.1", 5000);

let app = new Vue({ 
	el: '#root',
	data(){
		return {
			timedActions: [],
			obj: {
				activeTab: 0,
			},
			controllers:[{				
				title: "Timed Actions",
				fcn: function() {
					
				},
				template:""//for later
			
			},
			{
				title: "Other Tab",
				fcn: function() {
					
				},
				template:""//for later
			}]
		}
	},
	computed:{
	},
	methods:{		
		onGetTimedActions() {
            timedActions = reflectedService.utils.getTimedActions().then(response => {
				app.timedActions = response.actionList;
			});
		},
		onRemoveTimedAction(actionID) {
			reflectedService.utils.removeTimedAction(actionID)
		}
	}
})

setInterval(app.onGetTimedActions, 500);