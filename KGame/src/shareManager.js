// JavaScript Document
function ()
{
	this.userID="";
	this.matchID="";
	this.messageCallBackFunction=null;
	this.connectErrorCallBackFunction=null;	//服务器连接失败的回调函数
	ShareManager.instance=this;
}

ShareManager.prototype.instance=null;

//通过URL地址传递分享参数
ShareManager.prototype.ShareLogin=function(messageCallBackFunction,connectErrorCallBackFunction)
{
	this.messageCallBackFunction=messageCallBackFunction;
	this.connectErrorCallBackFunction=connectErrorCallBackFunction;
	this.ConnectServer();
}

//通过URL登录都是走的这个函数
ShareManager.prototype.ConnectServer=function()
{
	if(gSocketConn==null)
	{
		gSocketConn=new SocketConn();
	}
	
	if(gSocketConn.isconnected==false)
	{
		gSocketConn.RegisterEvent("onopen",this.ConnectedCallBack);
		gSocketConn.RegisterEvent("onerror",this.ErrorConnectCallBack);
		//gSocketConn.Connect('ws://222.66.97.203:5003/');
		//gSocketConn.Connect('ws://180.169.108.231:5003/');
		gSocketConn.Connect('ws://192.168.16.250:8484/');
	}
	else
	{
		console.log("链接服务器失败！！！！！ ConnectServer");
		
	}
}

//连接失败，可能服务器开启
ShareManager.prototype.ErrorConnectCallBack=function()
{
	var self=ShareManager.instance;
	gSocketConn.UnRegisterEvent("onopen",self.ConnectedCallBack);
	gSocketConn.UnRegisterEvent("onerror",self.ErrorConnectCallBack);
	if(self.connectErrorCallBackFunction!=null)
	{
		self.connectErrorCallBackFunction();
	}
}

//连接成功
ShareManager.prototype.ConnectedCallBack=function()
{
	console.log("connectedCallBack");
	var self=ShareManager.instance;
	gSocketConn.UnRegisterEvent("onopen",self.ConnectedCallBack);
	gSocketConn.UnRegisterEvent("onerror",self.ErrorConnectCallBack);
	self.ShareLogin();
}

ShareManager.prototype.ShareLogin=function()
{
	if(this.operationType==1 || this.operationType==2)
	{
		var self=this;
		gSocketConn.RegisterEvent("onmessage",this.LoginOrQucikLoginMessageCallback);
		if(this.operationType==1)
		{
			//登录
			gSocketConn.Login(this.username,this.password,this.source);
		}
		else if(this.operationType==2)
		{
			//注册并登录
			gSocketConn.QuickLogin();
		}
	}
}

ShareManager.prototype.LoginOrQucikLoginMessageCallback=function(message)
{
	var packet=Packet.prototype.Parse(message);
	if(packet==null) return;
	var self=ShareManager.instance;
	gSocketConn.UnRegisterEvent("onmessage",self.LoginOrRegisterMessageCallback);
	if(self.messageCallBackFunction!=null)
	{
		if(packet.msgType=="1" || packet.msgType=="2" || packet.msgType=="B" || packet.msgType=="C")
		{
			self.messageCallBackFunction(packet);
		}
	}
}


	
