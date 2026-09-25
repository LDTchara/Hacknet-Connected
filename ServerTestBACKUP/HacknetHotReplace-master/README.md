# HacknetHotReplace
Hacknet热替换扩展文件支持

[English](README_en.md)



## 前置要求

您需要安装[Pathfinder](https://github.com/Arkhist/Hacknet-Pathfinder)后才可使用本模组



## 用法

该插件必须使用`全局安装`：

解压Release包，将其中的dll文件复制到: `Hacknet.exe文件所在目录/BepInEx/plugins`下即可。



## 热替换

当前支持的所有热替换功能有：

* 热重载Computer
* 连接一台指定的Computer并授予管理员权限
* 热重载Mission文件
* 热重载Action文件
* 热重载Faction文件
* 热重载Theme文件
* 热重载People文件（一次性所有People文件全部重新加载）
* 动态执行Action
* 打印当前游戏OS信息
* 打印指定的计算机信息
* 定位绘制元素的调用堆栈



全局安装完成后当您启动游戏后，热替换插件会作为服务端监听系统端口，当客户端发送指令时，插件会执行指令并返回结果给客户端。

当游戏启动后，热替换插件会把自身监听的端口写入到windows注册表中以便客户端获取端口发送指令。并且只要插件在运行，他会每间隔2秒向注册表写入当前最新的时间戳（以毫秒为单位），已经客户端知道服务是否可用。

1. 监听端口

   windows:  注册表项：`HKEY_CURRENT_USER\Software\Hacknet`，Key=`HotReplaceServerPort`

   linux:   ~/.HacknetHotReplace/HotReplaceServerOnlineTimespan

2. 实时写入时间戳

   windows: 注册表项：`HKEY_CURRENT_USER\Software\Hacknet`，Key=`HotReplaceServerOnlineTimespan`

   linux:  ~/.HacknetHotReplace/HotReplaceServerPort



### 客户端消息

客户端使用UDP协议将数据按照以下格式发送给插件即可。

ip: `127.0.0.1`

port: `读取注册表：` `HKEY_CURRENT_USER\Software\Hacknet`，key=`HotReplaceServerPort`



请求消息格式

```xml
<Command>
      <CommandType>消息类型</CommandType>
      <MsgGuid>消息唯一id(客户端生成)</MsgGuid>
      <Payload>自定义消息负载</Payload>
 </Command>
```

响应消息格式

```xml
<CommandResponse>
      <CommandType>消息类型</CommandType>
      <MsgGuid>消息唯一id(客户端生成)</MsgGuid>
      <Success>指令是否执行成功true/false</Success>
      <Payload>响应数据</Payload>
 </CommandResponse>
```



#### 打开并进入指定扩展消息

```xml
<!-- 请求 -->
<Command>
    <CommandType>EnterExtension</CommandType>
    <MsgGuid>71315d64-e676-4ff9-98eb-601a6f0c91f3</MsgGuid>
    <Payload>
        <ExtensionFolder>扩展目录绝对路径</ExtensionFolder>
        <NeedApproval>是否需要用户确认(true/false)</NeedApproval>
        <EnterNoAccount>是否直接进入无账户模式(true/false)</EnterNoAccount>
    </Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>EnterExtension</CommandType>
  <MsgGuid>8044a465-6a8e-436a-ad27-481da62effd1</MsgGuid>
  <Success>True</Success>
  <Payload>success</Payload>
</CommandResponse>
```



#### 热重载计算机消息

```xml
<!-- 请求 -->
<Command>
    <CommandType>HotReloadComputer</CommandType>
    <MsgGuid>e75e94d8-c021-4eec-a19c-533d04700233</MsgGuid>
    <Payload>
        <ComputerPath>计算机文件绝对路径</ComputerPath>
    </Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>HotReloadComputer</CommandType>
  <MsgGuid>e75e94d8-c021-4eec-a19c-533d04700233</MsgGuid>
  <Success>True</Success>
  <Payload>success</Payload>
</CommandResponse>
```



#### 连接计算机并授予管理员权限

```xml
<!-- 请求 -->
<Command>
    <CommandType>ConnectComputer</CommandType>
    <MsgGuid>f8982775-c7ef-4ad5-b9f5-dc26c5244b89</MsgGuid>
    <Payload>
        <ComputerId>计算机ID</ComputerId>
    </Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>ConnectComputer</CommandType>
  <MsgGuid>f8982775-c7ef-4ad5-b9f5-dc26c5244b89</MsgGuid>
  <Success>True</Success>
  <Payload>success</Payload>
</CommandResponse>
```



#### 热重载当前Mission文件

```xml
<!-- 请求 -->
<Command>
    <CommandType>HotReloadMission</CommandType>
    <MsgGuid>9710e98e-027c-4871-b961-bc7f82b6e872</MsgGuid>
    <Payload>
        <MissionPath>Mission文件绝对路径</MissionPath>
        <ClearOldAction>(是否清除当前os内的所有action)true/false</ClearOldAction>
    </Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>HotReloadMission</CommandType>
  <MsgGuid>9710e98e-027c-4871-b961-bc7f82b6e872</MsgGuid>
  <Success>True</Success>
  <Payload>success</Payload>
</CommandResponse>
```



#### 热重载当前Action文件

```xml
<!-- 请求 -->
<Command>
    <CommandType>HotReloadAction</CommandType>
    <MsgGuid>9d263fbb-3f25-4bfd-ac47-0974446ec3e5</MsgGuid>
    <Payload>
        <ActionPath>Action文件绝对路径</ActionPath>
        <ClearOldAction>(是否清除当前os内的所有action)true/false</ClearOldAction>
    </Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>HotReloadAction</CommandType>
  <MsgGuid>9d263fbb-3f25-4bfd-ac47-0974446ec3e5</MsgGuid>
  <Success>True</Success>
  <Payload>success</Payload>
</CommandResponse>
```



#### 重载当前Theme文件

```xml
<!-- 请求 -->
<Command>
    <CommandType>HotReloadTheme</CommandType>
    <MsgGuid>89b4372f-5d44-47c1-a452-9690741db71a</MsgGuid>
    <Payload>
        <ThemePath>主题文件绝对路径</ThemePath>
    </Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>HotReloadTheme</CommandType>
  <MsgGuid>89b4372f-5d44-47c1-a452-9690741db71a</MsgGuid>
  <Success>True</Success>
  <Payload>success</Payload>
</CommandResponse>
```



#### 重载当前Faction文件

```xml
<!-- 请求 -->
<Command>
    <CommandType>HotReloadFaction</CommandType>
    <MsgGuid>161b41be-fdbd-4d12-9947-0f4b79d046e9</MsgGuid>
    <Payload>
        <FactionPath>faction文件绝对路径</FactionPath>
    </Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>HotReloadFaction</CommandType>
  <MsgGuid>161b41be-fdbd-4d12-9947-0f4b79d046e9</MsgGuid>
  <Success>True</Success>
  <Payload>success</Payload>
</CommandResponse>
```



#### 重载所有People文件

```xml
<!-- 请求 -->
<Command>
    <CommandType>HotReloadPeople</CommandType>
    <MsgGuid>be524cea-a6a5-4654-8687-84ad654fbdd4</MsgGuid>
    <Payload></Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>HotReloadPeople</CommandType>
  <MsgGuid>be524cea-a6a5-4654-8687-84ad654fbdd4</MsgGuid>
  <Success>True</Success>
  <Payload>success</Payload>
</CommandResponse>
```



#### 动态执行Action

```xml
<!-- 请求 -->
<Command>
    <CommandType>ExecuteAction</CommandType>
    <MsgGuid>d59de976-3704-47cc-a0a2-fb628beb5efe</MsgGuid>
    <Payload>
        <!-- 在ActionXmlContent内部存放需要动态执行的Action -->
        <ActionXmlContent>
            <ShowModule2 Names="[ALL]"></ShowModule2>
        </ActionXmlContent>
    </Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>HotReloadPeople</CommandType>
  <MsgGuid>be524cea-a6a5-4654-8687-84ad654fbdd4</MsgGuid>
  <Success>True</Success>
  <Payload>success</Payload>
</CommandResponse>
```



#### 打印OS信息

```xml
<!-- 请求 -->
<Command>
    <CommandType>PrintOS</CommandType>
    <MsgGuid>42c1e548-adf2-434f-9bfb-9e68d4b36611</MsgGuid>
    <Payload></Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>PrintOS</CommandType>
  <MsgGuid>42c1e548-adf2-434f-9bfb-9e68d4b36611</MsgGuid>
  <Success>True</Success>
   <!-- 内部是生成的os存档(不包括computer) -->
  <Payload>&lt;?xml version="1.0" encoding="utf-16"?&gt;
&lt;HacknetSave generatedMissionCount="0" Username="fengx" Language="zh-cn" DLCMode="false" DisableEmailIcon="false"&gt;
  &lt;HacknetFontReplace ActiveFontGroup="default" /&gt;
  &lt;DefaultUser username="fengx" password="password" type="1" know="true" /&gt;
  &lt;CustomerModules /&gt;
  &lt;DLC Active="False" LoadedContent="False"&gt;
    &lt;Flags OriginalFaction="entropy" /&gt;
    &lt;OriginalVisibleNodes&gt;&lt;/OriginalVisibleNodes&gt;
    &lt;ConditionalActions&gt;
      &lt;HasFlags requiredFlags="flag1,flag2"&gt;
        &lt;RunFunction FunctionName="addFlags:test1" FunctionValue="0" Delay="0" DelayHost="" /&gt;
        &lt;ChangeActiveFontGroup Name="desc" /&gt;
      &lt;/HasFlags&gt;
    &lt;/ConditionalActions&gt;
  &lt;/DLC&gt;
  &lt;Flags&gt;ExtensionFirstBootComplete,FirstAlertComplete&lt;/Flags&gt;
  &lt;mission next="NONE" goals="d:\program files (x86)\steam\steamapps\common\Hacknet\Extensions\Apocalypse/Missions/Mission-Start.xml" reqRank="0" activeCheck="true"&gt;
    &lt;email sender="发送者222" subject="主题22222"&gt;123邮件内容邮件内容{color: Red, fontGroup: desc}aa11 223{/}邮件内容邮件内容123
邮件内容邮件内容邮件内容邮件内容邮件{fontGroup: desc}665 14{/}内容邮件内容邮件内容
邮件内容邮件内容邮件内容邮件内容邮件内容邮件内容邮件内容
{color: Red, fontGroup: desc}邮件内容邮件内容邮件内容邮件内容邮件内容邮件内容{color: Blue}邮件  23a   内容{/}你好{/}

        &lt;/email&gt;
    &lt;endFunc val="1" name="addRank" /&gt;
    &lt;posting title=""&gt;&lt;/posting&gt;
  &lt;/mission&gt;
  &lt;branchMissions /&gt;
  &lt;AllFactions current="TestFac"&gt;
    &lt;CustomFaction name="TestFac" id="TestFac" neededVal="100" playerVal="1" playerHasPassed="false"&gt;
      &lt;Action ValueRequired="0"&gt;
        &lt;ChangeActiveFontGroup Name="desc" /&gt;
      &lt;/Action&gt;
    &lt;/CustomFaction&gt;
  &lt;/AllFactions&gt;
  &lt;other music="Music/Ryan3" homeNode="entropy00" homeAssetsNode="entropy01" /&gt;
&lt;/HacknetSave&gt;</Payload>
</CommandResponse>
```



#### 打印计算机信息

```xml
<!-- 请求 -->
<Command>
    <CommandType>PrintComputer</CommandType>
    <MsgGuid>d181d2e9-7a65-48a7-aed5-847e262e6ef8</MsgGuid>
    <Payload>
        <!-- 计算机ID -->
        <ComputerId>playerComp</ComputerId>
    </Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>PrintComputer</CommandType>
  <MsgGuid>d181d2e9-7a65-48a7-aed5-847e262e6ef8</MsgGuid>
  <Success>True</Success>
   <!-- 内部是生成的括computer存档 -->
  <Payload>&lt;computer name="fengx Workstation" ip="81.243.206.126" type="4" spec="player" id="playerComp"&gt;
  &lt;location x="0.4724679" y="0.9782055" /&gt;
  &lt;security level="3" traceTime="300" adminIP="81.243.206.126" portsToCrack="100" /&gt;
  ...........
  </Payload>
</CommandResponse>
```



#### 打印游戏绘制元素的堆栈信息

发生消息后再游戏中，使用中键单击界面元素，即可定位到该元素绘制方法的调用堆栈

```xml
<!-- 请求 -->
<Command>
    <CommandType>AnalysisDrawCall</CommandType>
    <MsgGuid>5489449c-c463-4ac3-a7ed-e166ac8de4c8</MsgGuid>
    <Payload>
        <!--
 			graphics: 定位图像信息
			text: 定位文本信息
 		-->
        <Type>graphics</Type>
        <!-- Type=text的时候此处为需要寻找的文本信息 -->
        <Content></Content>
    </Payload>
</Command>

<!-- 响应 -->
<CommandResponse>
  <CommandType>AnalysisDrawCall</CommandType>
  <MsgGuid>5489449c-c463-4ac3-a7ed-e166ac8de4c8</MsgGuid>
  <Success>True</Success>
  <Payload>[DRAW CALL] [IL:-1] GLOBAL.DMD&lt;Microsoft.Xna.Framework.Graphics.SpriteBatch::Draw&gt;
[DRAW CALL] [IL:363] Hacknet.Effects.HexGridBackground.Draw
[DRAW CALL] [IL:-1] GLOBAL.DMD&lt;Hacknet.MainMenu::Draw&gt;
[DRAW CALL] [IL:73] Hacknet.ScreenManager.Draw
[DRAW CALL] [IL:112] Microsoft.Xna.Framework.Game.Draw
[DRAW CALL] [IL:-1] GLOBAL.DMD&lt;Hacknet.Game1::Draw&gt;
[DRAW CALL] [IL:524] Microsoft.Xna.Framework.Game.Tick
[DRAW CALL] [IL:1474] Microsoft.Xna.Framework.SDL2_FNAPlatform.RunLoop
[DRAW CALL] [IL:55] Microsoft.Xna.Framework.Game.Run
[DRAW CALL] [IL:-1] GLOBAL.DMD&lt;Hacknet.Program::Main&gt;
</Payload>
```





## VsCode插件支持

[HacknetExtensionHelper](https://marketplace.visualstudio.com/items?itemName=fengxu30338.hacknetextensionhelper)插件已经包含了该pf插件的所有功能，您可以在vscode中安装该插件以便您更简单的调用以上功能。

如果您有其他需求的话可以新增一个消息处理器，将您的代码合并到该仓库，需要vscode插件提供调用的话，您可以在本仓库提一个issue留言给我，我有空的话会处理。





## 关于

若您使用了本模组请在模组说明处注明来源。
