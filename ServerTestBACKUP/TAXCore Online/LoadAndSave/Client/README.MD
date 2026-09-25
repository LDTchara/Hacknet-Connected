目前需要补充的是：需要在选择好server和账户（注意，先选择server后选择账户）后将username和password和账户服务器的全局id存储到主文件的全局变量中，然后调用SaveToClientAccountStructure（os）存储一遍，所有的东西大概会被存到        TAXCoreOnline.CurrentAccount
（一定记得设置isClient=true;）
还有：存储当前server到AvailableServer CurrentServer
    public string HostName;
    public string IP;
    public int Port;
    public HashSet<ClientAccountStructure> Accounts; 这些是配置。
然后将当前的account存到这个hashset里面去（注意的是，每次新建账户的触发这个存储我还没写。。）
全局变量Hosts存储了所有的服务器，记得在首次连接或者之类的时候更新它类型为AvailableServer