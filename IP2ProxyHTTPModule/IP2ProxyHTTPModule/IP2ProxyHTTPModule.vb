'--------------------------------------------------------------------------
' Title        : IP2Proxy HTTP Module
' Description  : This module lookup the IP2Proxy database from an IP address to determine if it was being used as open proxy, web proxy, VPN anonymizer and TOR exits.
' Requirements : .NET 3.5 Framework (due to IIS limitations, .NET 3.5 module is the easiest to deploy)
' IIS Versions : 7.0, 7.5, 8.0 & 8.5
'
' Author       : IP2Location.com
' URL          : http://www.ip2location.com
' Email        : sales@ip2location.com
'
' Copyright (c) 2002-2022 IP2Location.com
'
'---------------------------------------------------------------------------

Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Web
Imports System.Xml.Serialization

Public Class HTTPModule : Implements IHttpModule
    Public proxy As New IP2Proxy
    Private config As IP2ProxyConfig = Nothing
    Private result As ProxyResult
    Private Const configFile As String = "IP2Proxy-config.xml"
    Private globalConfig As String = Nothing
    Private baseDir As String = ""
    Public whitespace As Regex
    Public proxyDatabasePath As String = ""
    Private version As String = "3.2"

    Public Sub Dispose() Implements System.Web.IHttpModule.Dispose
        LogDebug.WriteLog("Exiting IP2Proxy HTTP Module")
        proxy = Nothing
        config = Nothing
    End Sub

    Public Sub Init(context As System.Web.HttpApplication) Implements System.Web.IHttpModule.Init
        Dim mydirectory As String = ""

        globalConfig = Environment.GetEnvironmentVariable("IP2ProxyHTTPModuleConfig")
        If globalConfig IsNot Nothing Then 'server level mode
            LogDebug.WriteLog("Global config: " & globalConfig)
            mydirectory = globalConfig
            If Not mydirectory.EndsWith("\") Then
                mydirectory &= "\"
            End If
        Else 'website level mode
            baseDir = AppDomain.CurrentDomain.BaseDirectory
            mydirectory = baseDir & "bin\" 'always assume config file in bin folder
        End If

        Try
            LogDebug.WriteLog("Starting IP2Proxy HTTP Module " & version)
            whitespace = New Regex("\s")

            config = ReadConfig(mydirectory & configFile)

            'Set BIN file path
            If globalConfig IsNot Nothing Then
                proxyDatabasePath = config.Settings.BINFile 'global BIN is always full path 
            Else
                proxyDatabasePath = baseDir & config.Settings.BINFile 'website BIN is always relative to website root folder
            End If

            If proxy.Open(proxyDatabasePath) = 0 Then
                AddHandler context.PreRequestHandlerExecute, AddressOf OnPreExecuteRequestHandler
            Else
                LogDebug.WriteLog("Init-Unable to read BIN file.")
            End If
        Catch ex As Exception
            LogDebug.WriteLog(ex.Message & vbNewLine & ex.StackTrace)
        End Try
    End Sub

    Public Sub OnPreExecuteRequestHandler(sender As Object, e As EventArgs)
        Dim app As HttpApplication = DirectCast(sender, HttpApplication)
        Dim request As HttpRequest = app.Context.Request
        Dim response As HttpResponse = app.Context.Response
        Dim myurl As String = request.Url.AbsoluteUri

        Dim myIP As String
        If config.Settings.CustomIPServerVariable.Trim <> "" Then
            myIP = request.ServerVariables.Item(config.Settings.CustomIPServerVariable.Trim)
        Else
            myIP = request.UserHostAddress
        End If

        ' output extra info so we know it is working
        LogDebug.WriteLog("Querying IP: " & myIP)
        result = proxy.GetAll(myIP)
        LogDebug.WriteLog("Query Status: " & result.Is_Proxy)
        LogDebug.WriteLog("Full URL: " & myurl)

        request.ServerVariables.Item("HTTP_X_IP2PROXY_COUNTRY_SHORT") = result.Country_Short
        request.ServerVariables.Item("HTTP_X_IP2PROXY_COUNTRY_LONG") = result.Country_Long
        request.ServerVariables.Item("HTTP_X_IP2PROXY_REGION") = result.Region
        request.ServerVariables.Item("HTTP_X_IP2PROXY_CITY") = result.City
        request.ServerVariables.Item("HTTP_X_IP2PROXY_ISP") = result.ISP
        request.ServerVariables.Item("HTTP_X_IP2PROXY_PROXY_TYPE") = result.Proxy_Type
        request.ServerVariables.Item("HTTP_X_IP2PROXY_IS_PROXY") = result.Is_Proxy
        request.ServerVariables.Item("HTTP_X_IP2PROXY_DOMAIN") = result.Domain
        request.ServerVariables.Item("HTTP_X_IP2PROXY_USAGE_TYPE") = result.Usage_Type
        request.ServerVariables.Item("HTTP_X_IP2PROXY_ASN") = result.ASN
        request.ServerVariables.Item("HTTP_X_IP2PROXY_AS") = result.AS
        request.ServerVariables.Item("HTTP_X_IP2PROXY_LAST_SEEN") = result.Last_Seen
        request.ServerVariables.Item("HTTP_X_IP2PROXY_THREAT") = result.Threat
        request.ServerVariables.Item("HTTP_X_IP2PROXY_PROVIDER") = result.Provider
    End Sub

    Private Function ReadConfig(ByVal filename As String) As IP2ProxyConfig
        ' Create an instance of the XmlSerializer class; 
        ' specify the type of object to be deserialized. 
        Dim serializer As New XmlSerializer(GetType(IP2ProxyConfig))
        ' If the XML document has been altered with unknown 
        ' nodes or attributes, handle them with the 
        ' UnknownNode and UnknownAttribute events. 
        AddHandler serializer.UnknownNode, AddressOf SerializerUnknownNode
        AddHandler serializer.UnknownAttribute, AddressOf SerializerUnknownAttribute

        ' All these just to make sure XML is following our case sensitivity.
        Dim line As String
        Dim sr2 As StringReader = Nothing
        Dim regexstr As New List(Of String)
        Dim normalelem As String
        normalelem = "Settings|BIN_File"
        Dim elem As String
        Dim elem2 As String

        regexstr.Add("(</?)(IP2Proxy_Configuration)([>|\s])") 'main element

        For Each elem In normalelem.Split("|")
            regexstr.Add("(</?)(" & elem & ")(>)")
        Next

        Try
            'Have to make sure all the XML supplied by user is correct case
            Using sr As New StreamReader(filename)
                line = sr.ReadToEnd()
            End Using

            'Fix the XML here using replace
            For Each elem In regexstr
                elem2 = elem.Replace(")(", "#").Split("#")(1) 'to get the tag name with specific case sensitivity
                line = Regex.Replace(line, elem, "$1" & elem2 & "$3", RegexOptions.IgnoreCase)
            Next

            ' Declare an object variable of the type to be deserialized. 
            Dim config As IP2ProxyConfig

            ' Use the Deserialize method to restore the object's state with 
            ' data from the XML document.
            sr2 = New StringReader(line)

            config = CType(serializer.Deserialize(sr2), IP2ProxyConfig)

            Return config
        Catch ex As Exception
            LogDebug.WriteLog(ex.Message)
            Throw 'special case so need to throw here to stop the main process
        Finally
            If sr2 IsNot Nothing Then
                sr2.Close()
            End If
        End Try
    End Function

    Private Sub SerializerUnknownNode(sender As Object, e As XmlNodeEventArgs)
        LogDebug.WriteLog("Unknown Node:" & e.Name & vbTab & e.Text)
    End Sub


    Private Sub SerializerUnknownAttribute(sender As Object, e As XmlAttributeEventArgs)
        Dim attr As Xml.XmlAttribute = e.Attr
        LogDebug.WriteLog("Unknown attribute " & attr.Name & "='" & attr.Value & "'")
    End Sub

End Class
