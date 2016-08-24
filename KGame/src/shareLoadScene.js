// JavaScript Document
var shareLoadScene = SceneBase.extend(
{
	backgroundLayer:null,		//背景层
	
	
	titleSprite:null,
	
	loadTime:null,
	
	ctor: function ()
	{
		this._super();
		this.backgroundLayer=null;
	},
	
	onEnter:function () 
	{
		this._super();
		
		
		var size = cc.director.getWinSize();
		this.backgroundLayer=new cc.LayerColor(cc.color(15,96,148, 255));
		this.backgroundLayer.ignoreAnchorPointForPosition(false);  
		this.backgroundLayer.setPosition(size.width / 2, size.height / 2);  
		this.addChild(this.backgroundLayer, 1,this.backgroundLayer.getTag());
		
		this.titleSprit=new cc.Sprite.create("res/title.png");
		this.titleSprit.setPosition(this.width / 2, this.height / 2);
		this.titleSprit.setScale(1);
		this.addChild(this.titleSprit, 2,this.titleSprit.getTag());
		
		var self=this;
		this.showProgress();
		loadTime=new Date().getTime();
	},
	
	
	connectErrorCallBack:function()
	{
		var self=this;
		//setTimeout(function(){
		//	self.stopProgress();
		//	self.showMessageBox("服务器连接失败，请稍候再试！",function(){self.messageBoxClosed();});
		//	},2000);
		this.stopProgress();
		this.showMessageBox("服务器连接失败，请稍候再试！",function(){this.messageBoxClosed();});
	},
	
	messageCallback:function(packet)
	{
		console.log("login scene message callback packet.msgType="+packet.msgType+" content="+packet.content);
		var self=this;
		if(packet.msgType=="1")
		{
			gPlayerName=packet.content;
			//登录成功
			this.OnLogined(packet.content);
		}
		else if(packet.msgType=="H")
		{
			//分享成功
			this.stopProgress();
			this.OnLogined(packet.content);
			console.log(packet.content);
			//gLoginManager.Login(this.username,this.password,null,function(packet){self.messageCallback(packet)},function(){self.connectErrorCallBack()});
		}
	},
	
	OnLogined:function(username)
	{
		this.moveToNextScene();
	},
	
	
	//一般是登录名或者密码错误之类的框关闭以后
	messageBoxClosed:function()
	{
		//this.showOrHideTextBoxUILabel(false);
	},

	
	moveToNextScene:function()
	{
		
		var self=this;
		var endTime=new Date().getTime();
		if(endTime-loadTime>5000)
		{
			this.moveToNextSceneCallBack();
		}
		else
		{
			setTimeout(function(){self.moveToNextSceneCallBack(),5000-(endTime-loadTime);});
		}
	},
	
	moveToNextSceneCallBack:function()
	{
		console.log("登录成功，准备切换到下一个场景");
		this.stopProgress();
		var klineSceneNext=new KLineScene();
		klineSceneNext.onEnteredFunction=function(){
			klineSceneNext.showProgress();
		};
		
		var userID=getQueryStringByName("userID");
		var matchID=getQueryStringByName("matchID");
		
		gSocketConn.RegisterEvent("onmessage",klineSceneNext.messageCallBack);
		gSocketConn.ShareMessage(userID,matchID);
		//cc.director.runScene(cc.TransitionFade.create(0.5,klineSceneNext,cc.color(255,255,255,255)));
		cc.director.runScene(klineSceneNext);
		console.log("切换场景调用完毕");
	}
});