'---------------------------------------------------------------------------
' Author       : IP2Location.com
' URL          : http://www.ip2location.com
' Email        : sales@ip2location.com
'
' Copyright (c) 2002-2020 IP2Location.com
'---------------------------------------------------------------------------

Imports System.Xml
Imports System.Xml.Serialization

'NOTE: The XMLRootAttribute and XMLElement are for renaming the XML output tags.

<XmlRootAttribute("IP2Proxy_Configuration")> _
Public Class IP2ProxyConfig
    Public Settings As Settings
End Class

Public Class Settings
    <XmlElement("BIN_File")> _
    Public BINFile As String

    <XmlElement("Custom_IP_Server_Variable")> _
    Public CustomIPServerVariable As String
End Class
