'---------------------------------------------------------------------------
' Author       : IP2Location.com
' URL          : http://www.ip2location.com
' Email        : sales@ip2location.com
'
' Copyright (c) 2002-2022 IP2Location.com
'---------------------------------------------------------------------------

Imports System.Xml.Serialization

'NOTE: The XMLRoot and XMLElement are for renaming the XML output tags.

<XmlRoot("IP2Proxy_Configuration")>
Public Class IP2ProxyConfig
    Public Settings As Settings
End Class

Public Class Settings
    <XmlElement("BIN_File")>
    Public BINFile As String

    <XmlElement("Custom_IP_Server_Variable")>
    Public CustomIPServerVariable As String
End Class
