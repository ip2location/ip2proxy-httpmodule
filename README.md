# IP2Proxy HTTP Module

This IIS managed module allows user to query an IP address if it was being used as VPN anonymizer, open proxies, web proxies, Tor exits, data center, web hosting (DCH) range, search engine robots (SES) and residential (RES). It lookup the proxy IP address from **IP2Proxy BIN Data** file. This data file can be downloaded at

* Free IP2Proxy BIN Data: https://lite.ip2location.com
* Commercial IP2Proxy BIN Data: https://www.ip2location.com/database/ip2proxy


## Requirements

* Visual Studio 2010 or later.
* Microsoft .NET 3.5 framework.
* [IntX](https://www.nuget.org/packages/IntX/)
* [Microsoft ILMerge](https://www.microsoft.com/en-my/download/details.aspx?id=17630)

Supported Microsoft IIS Versions: 7.0, 7.5, 8.0, 8.5, 10.0 (website needs to be running under a .NET 2.0 application pool in integrated mode)


## Compilation

Just open the solution file in Visual Studio and compile. Or just use the IP2ProxyHTTPModule.dll in the dll folder.

**NOTE: After compilation, the final IP2ProxyHTTPModule.dll will be in the merged folder as the post-build event will merge the IntXLib.dll with the original IP2ProxyHTTPModule.dll to make it easier for deployment.**

___

## Installation & Configuration

**NOTE: You can choose to install the IP2Proxy HTTP Module in either per website mode or per server mode.**

If you install in per website mode, you will need to install and configure for every website that you wish to add the IP2Proxy feature.
If you install in per server mode, you just need to install and configure once and all websites hosted on that machine will be able to use IP2Proxy.

### Installation & Configuration (per website mode)

1. Copy the IP2ProxyHTTPModule.dll, IP2Proxy-config.xml and the BIN data file to the bin folder of your website.

2. Modify your web.config as below:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
    <system.webServer>
        <modules runAllManagedModulesForAllRequests="true">
            <add name="IP2ProxyModule" type="IP2Proxy.HTTPModule" />
        </modules>
    </system.webServer>
</configuration>
```

3. Open the IP2Proxy-config.xml in your bin folder using any text editor. Fill in the <BIN_File> tag with the path to your BIN data file and remove the HTTP_X_FORWARDED_FOR if your website is not behind a proxy. Save your changes.

```xml
<?xml version="1.0" encoding="utf-8"?>
<IP2Proxy_Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Settings>
    <BIN_File>bin\your_database_file.BIN</BIN_File>
    <Custom_IP_Server_Variable>HTTP_X_FORWARDED_FOR</Custom_IP_Server_Variable>
  </Settings>
</IP2Proxy_Configuration>
```

### Installation & Configuration (per server mode)

1. Create a new folder.

2. Copy the IP2ProxyHTTPModule.dll, IP2Proxy-config.xml and the BIN data file to that folder.

3. Create a Windows environment system variable to store the path of the new folder.
   1. Open the Control Panel then double-click on System then click on Advanced System Settings.
   2. Click on the Environment Variables button to open up the Environment Variable settings.
   3. Under System variables, create a new variable called IP2ProxyHTTPModuleConfig and set the value to the full path of the new folder.

4. Create a PowerShell script called installgac.ps1 and paste the following code into it.

```powershell
Set-location "C:\<new folder>"
[System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
$publish = New-Object System.EnterpriseServices.Internal.Publish
$publish.GacInstall("C:\<new folder>\IP2ProxyHTTPModule.dll")
iisreset
```

5. Create a PowerShell script called uninstallgac.ps1 and paste the following code into it.

```powershell
Set-location "C:\<new folder>"
[System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
$publish = New-Object System.EnterpriseServices.Internal.Publish
$publish.GacRemove("C:\<new folder>\IP2ProxyHTTPModule.dll")
iisreset
```

6. In both scripts, edit the 2 lines containing the path to the full path for your new folder then save the scripts.

7. Run installgac.ps1 to install the dll into the GAC. Keep the uninstallgac.ps1 in case you need to uninstall the dll. 

8. Installing the module in IIS.
   1. Open the IIS Manager then navigate to the server level settings and double-click on the Modules icon.
   2. In the Modules settings, click on the Add Managed Module at the right-hand side.
   3. Key in IP2ProxyHTTPModule for the Name and select IP2Proxy.HTTPModule as the Type.
   4. Click OK then restart IIS to complete the installation.

9. Open the IP2Proxy-config.xml in your new folder using any text editor. Fill in the <BIN_File> tag with the absolute path to your BIN data file and remove the HTTP_X_FORWARDED_FOR if your website is not behind a proxy. Save your changes.

```xml
<?xml version="1.0" encoding="utf-8"?>
<IP2Proxy_Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Settings>
    <BIN_File>C:\<new folder>\your_database_file.BIN</BIN_File>
    <Custom_IP_Server_Variable>HTTP_X_FORWARDED_FOR</Custom_IP_Server_Variable>
  </Settings>
</IP2Proxy_Configuration>
```

___

## Usage

Below are the server variables set by the IP2Proxy HTTP Module. You can use any programming languages to read the server variables.

|Variable Name|Description|
|---|---|
|HTTP_X_IP2PROXY_IS_PROXY|Possible values:<ul><li>-1 : errors</li><li>0 : not a proxy</li><li>1 : a proxy</li><li>2 : a data center IP address or search engine robot</li></ul>|
|HTTP_X_IP2PROXY_PROXY_TYPE|Proxy type. Please visit [IP2Location](https://www.ip2location.com/database/px10-ip-proxytype-country-region-city-isp-domain-usagetype-asn-lastseen-threat-residential) for the list of proxy types supported.|
|HTTP_X_IP2PROXY_COUNTRY_SHORT|ISO3166-1 country code (2-digits) of the proxy.|
|HTTP_X_IP2PROXY_COUNTRY_LONG|ISO3166-1 country name of the proxy.|
|HTTP_X_IP2PROXY_REGION|ISO3166-2 region name of the proxy. Please visit [ISO3166-2 Subdivision Code](https://www.ip2location.com/free/iso3166-2) for the information of ISO3166-2 supported|
|HTTP_X_IP2PROXY_CITY|City name of the proxy.|
|HTTP_X_IP2PROXY_ISP|ISP name of the proxy.|
|HTTP_X_IP2PROXY_DOMAIN|Domain name of the proxy.|
|HTTP_X_IP2PROXY_USAGE_TYPE|Usage type. Please visit [IP2Location](https://www.ip2location.com/database/px10-ip-proxytype-country-region-city-isp-domain-usagetype-asn-lastseen-threat-residential) for the list of usage types supported.|
|HTTP_X_IP2PROXY_ASN|Autonomous system number of the proxy.|
|HTTP_X_IP2PROXY_AS|Autonomous system name of the proxy.|
|HTTP_X_IP2PROXY_LAST_SEEN|Number of days that the proxy was last seen.|
|HTTP_X_IP2PROXY_THREAT|Threat type of the proxy.|
|HTTP_X_IP2PROXY_PROVIDER|Provider of the proxy.|

___

## Sample Codes

### ASP.NET (VB)

```vb.net
Private Sub ShowServerVariable()
    Response.Write(Request.ServerVariables("REMOTE_ADDR") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_IS_PROXY") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_PROXY_TYPE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_COUNTRY_SHORT") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_COUNTRY_LONG") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_REGION") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_CITY") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_ISP") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_DOMAIN") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_USAGE_TYPE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_ASN") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_AS") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_LAST_SEEN") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_THREAT") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_PROVIDER") & "<br>")
End Sub
```

### ASP.NET (C#)

```csharp
private void ShowServerVariable()
{
   Response.Write(Request.ServerVariables["REMOTE_ADDR"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_IP2PROXY_IS_PROXY"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_IP2PROXY_PROXY_TYPE"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_IP2PROXY_COUNTRY_SHORT"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_IP2PROXY_COUNTRY_LONG"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_IP2PROXY_REGION"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_IP2PROXY_CITY"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_IP2PROXY_ISP"] + "\n");
   Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_DOMAIN") + "\n");
   Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_USAGE_TYPE") + "\n");
   Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_ASN") + "\n");
   Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_AS") + "\n");
   Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_LAST_SEEN") + "\n");
   Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_THREAT") + "\n");
   Response.Write(Request.ServerVariables("HTTP_X_IP2PROXY_PROVIDER") + "\n");
}
```

### ASP

```asp
<html>
<head>
    <title>IP2Proxy HTTP Module</title>
</head>
<body>
    <%=Request.ServerVariables("REMOTE_ADDR") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_IS_PROXY") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_PROXY_TYPE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_COUNTRY_SHORT") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_COUNTRY_LONG") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_REGION") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_CITY") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_ISP") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_DOMAIN") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_USAGE_TYPE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_ASN") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_AS") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_LAST_SEEN") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_THREAT") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_IP2PROXY_PROVIDER") & "<br>"%>
</body>
</html>
```

### PHP

```php
<html>
<head>
    <title>IP2Proxy HTTP Module</title>
</head>
<body>
<?php
    echo $_SERVER['REMOTE_ADDR'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_IS_PROXY'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_PROXY_TYPE'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_COUNTRY_SHORT'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_COUNTRY_LONG'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_REGION'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_CITY'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_ISP'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_DOMAIN'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_USAGE_TYPE'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_ASN'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_AS'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_LAST_SEEN'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_THREAT'] . "<br>";
    echo $_SERVER['HTTP_X_IP2PROXY_PROVIDER'] . "<br>";
?>
</body>
</html>
```
