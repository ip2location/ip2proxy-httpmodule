﻿'---------------------------------------------------------------------------
' Author       : IP2Location.com
' URL          : http://www.ip2location.com
' Email        : sales@ip2location.com
'
' Copyright (c) 2002-2022 IP2Location.com
'---------------------------------------------------------------------------

Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Globalization
Imports IntXLib ' installed via NuGet

Public Structure ProxyResult
    Public Is_Proxy As Integer
    Public Proxy_Type As String
    Public Country_Short As String
    Public Country_Long As String
    Public Region As String
    Public City As String
    Public ISP As String
    Public Domain As String
    Public Usage_Type As String
    Public ASN As String
    Public [AS] As String
    Public Last_Seen As String
    Public Threat As String
    Public Provider As String
End Structure

Public NotInheritable Class IP2Proxy
    Private _DBFilePath As String = ""
    Private ReadOnly _IndexArrayIPv4(65535, 1) As Integer
    Private ReadOnly _IndexArrayIPv6(65535, 1) As Integer
    Private ReadOnly _OutlierCase1 As New Regex("^:(:[\dA-F]{1,4}){7}$", RegexOptions.IgnoreCase)
    Private ReadOnly _OutlierCase2 As New Regex("^:(:[\dA-F]{1,4}){5}:(\d{1,3}\.){3}\d{1,3}$", RegexOptions.IgnoreCase)
    Private ReadOnly _OutlierCase3 As New Regex("^\d+$")
    Private ReadOnly _OutlierCase4 As New Regex("^([\dA-F]{1,4}:){6}(0\d+\.|.*?\.0\d+).*$")
    Private ReadOnly _OutlierCase5 As New Regex("^(\d+\.){1,2}\d+$")
    Private ReadOnly _IPv4MappedRegex As New Regex("^(.*:)((\d+\.){3}\d+)$")
    Private ReadOnly _IPv4MappedRegex2 As New Regex("^.*((:[\dA-F]{1,4}){2})$")
    Private ReadOnly _IPv4CompatibleRegex As New Regex("^::[\dA-F]{1,4}$", RegexOptions.IgnoreCase)
    Private _IPv4ColumnSize As Integer = 0
    Private _IPv6ColumnSize As Integer = 0

    Private _BaseAddr As Integer = 0
    Private _DBCount As Integer = 0
    Private _DBColumn As Integer = 0
    Private _DBType As Integer = 0
    Private _DBDay As Integer = 1
    Private _DBMonth As Integer = 1
    Private _DBYear As Integer = 1
    Private _BaseAddrIPv6 As Integer = 0
    Private _DBCountIPv6 As Integer = 0
    Private _IndexBaseAddr As Integer = 0
    Private _IndexBaseAddrIPv6 As Integer = 0
    Private _ProductCode As Integer = 0
    Private _ProductType As Integer = 0
    Private _FileSize As Integer = 0

    Private ReadOnly _FromBI As New IntX("281470681743360")
    Private ReadOnly _ToBI As New IntX("281474976710655")
    Private ReadOnly _FromBI2 As New IntX("42545680458834377588178886921629466624")
    Private ReadOnly _ToBI2 As New IntX("42550872755692912415807417417958686719")
    Private ReadOnly _FromBI3 As New IntX("42540488161975842760550356425300246528")
    Private ReadOnly _ToBI3 As New IntX("42540488241204005274814694018844196863")
    Private ReadOnly _DivBI As New IntX("4294967295")

    Private Const FIVESEGMENTS As String = "0000:0000:0000:0000:0000:"

    Private ReadOnly SHIFT64BIT As New IntX("18446744073709551616")
    Private ReadOnly MAX_IPV4_RANGE As New IntX("4294967295")
    Private ReadOnly MAX_IPV6_RANGE As New IntX("340282366920938463463374607431768211455")

    Private Const MSG_NOT_SUPPORTED As String = "NOT SUPPORTED"
    Private Const MSG_INVALID_IP As String = "INVALID IP ADDRESS"
    Private Const MSG_MISSING_FILE As String = "MISSING FILE"
    Private Const MSG_IPV6_UNSUPPORTED As String = "IPV6 ADDRESS MISSING IN IPV4 BIN"
    Private Const MSG_INVALID_BIN As String = "Incorrect IP2Proxy BIN file format. Please make sure that you are using the latest IP2Proxy BIN file."

    Public Enum IOModes
        IP2PROXY_FILE_IO = 1
    End Enum

    Public Enum Modes
        COUNTRY_SHORT = 1
        COUNTRY_LONG = 2
        REGION = 3
        CITY = 4
        ISP = 5
        PROXY_TYPE = 6
        IS_PROXY = 7
        DOMAIN = 8
        USAGE_TYPE = 9
        ASN = 10
        [AS] = 11
        LAST_SEEN = 12
        THREAT = 13
        PROVIDER = 14
        ALL = 100
    End Enum

    Private ReadOnly COUNTRY_POSITION() As Byte = {0, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3}
    Private ReadOnly REGION_POSITION() As Byte = {0, 0, 0, 4, 4, 4, 4, 4, 4, 4, 4, 4}
    Private ReadOnly CITY_POSITION() As Byte = {0, 0, 0, 5, 5, 5, 5, 5, 5, 5, 5, 5}
    Private ReadOnly ISP_POSITION() As Byte = {0, 0, 0, 0, 6, 6, 6, 6, 6, 6, 6, 6}
    Private ReadOnly PROXYTYPE_POSITION() As Byte = {0, 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2}
    Private ReadOnly DOMAIN_POSITION() As Byte = {0, 0, 0, 0, 0, 7, 7, 7, 7, 7, 7, 7}
    Private ReadOnly USAGETYPE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 8, 8, 8, 8, 8, 8}
    Private ReadOnly ASN_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 9, 9, 9, 9, 9}
    Private ReadOnly AS_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 10, 10, 10, 10, 10}
    Private ReadOnly LASTSEEN_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 11, 11, 11, 11}
    Private ReadOnly THREAT_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 12, 12}
    Private ReadOnly PROVIDER_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 13}

    Private COUNTRY_POSITION_OFFSET As Integer = 0
    Private REGION_POSITION_OFFSET As Integer = 0
    Private CITY_POSITION_OFFSET As Integer = 0
    Private ISP_POSITION_OFFSET As Integer = 0
    Private PROXYTYPE_POSITION_OFFSET As Integer = 0
    Private DOMAIN_POSITION_OFFSET As Integer = 0
    Private USAGETYPE_POSITION_OFFSET As Integer = 0
    Private ASN_POSITION_OFFSET As Integer = 0
    Private AS_POSITION_OFFSET As Integer = 0
    Private LASTSEEN_POSITION_OFFSET As Integer = 0
    Private THREAT_POSITION_OFFSET As Integer = 0
    Private PROVIDER_POSITION_OFFSET As Integer = 0

    Private COUNTRY_ENABLED As Boolean = False
    Private REGION_ENABLED As Boolean = False
    Private CITY_ENABLED As Boolean = False
    Private ISP_ENABLED As Boolean = False
    Private PROXYTYPE_ENABLED As Boolean = False
    Private DOMAIN_ENABLED As Boolean = False
    Private USAGETYPE_ENABLED As Boolean = False
    Private ASN_ENABLED As Boolean = False
    Private AS_ENABLED As Boolean = False
    Private LASTSEEN_ENABLED As Boolean = False
    Private THREAT_ENABLED As Boolean = False
    Private PROVIDER_ENABLED As Boolean = False

    'Description: Returns the module version
    Public Function GetModuleVersion() As String
        Dim Ver = Reflection.Assembly.GetExecutingAssembly().GetName().Version()
        Return Ver.Major & "." & Ver.Minor & "." & Ver.Build
    End Function

    'Description: Returns the package version
    Public Function GetPackageVersion() As String
        Return _DBType.ToString()
    End Function

    'Description: Returns the IP database version
    Public Function GetDatabaseVersion() As String
        If _DBYear = 0 Then
            Return ""
        Else
            Return "20" & _DBYear.ToString(CultureInfo.CurrentCulture()) & "." & _DBMonth.ToString(CultureInfo.CurrentCulture()) & "." & _DBDay.ToString(CultureInfo.CurrentCulture())
        End If
    End Function

    'Description: Returns an integer to state if is proxy
    Public Function IsProxy(IP As String) As Integer
        ' -1 is error
        '  0 is not a proxy
        '  1 is proxy except DCH and SES
        '  2 is proxy and DCH or SES
        Return ProxyQuery(IP, Modes.IS_PROXY).Is_Proxy
    End Function

    'Description: Returns a string for the country code
    Public Function GetCountryShort(IP As String) As String
        Return ProxyQuery(IP, Modes.COUNTRY_SHORT).Country_Short
    End Function

    'Description: Returns a string for the country name
    Public Function GetCountryLong(IP As String) As String
        Return ProxyQuery(IP, Modes.COUNTRY_LONG).Country_Long
    End Function

    'Description: Returns a string for the region name
    Public Function GetRegion(IP As String) As String
        Return ProxyQuery(IP, Modes.REGION).Region
    End Function

    'Description: Returns a string for the city name
    Public Function GetCity(IP As String) As String
        Return ProxyQuery(IP, Modes.CITY).City
    End Function

    'Description: Returns a string for the ISP name
    Public Function GetISP(IP As String) As String
        Return ProxyQuery(IP, Modes.ISP).ISP
    End Function

    'Description: Returns a string for the proxy type
    Public Function GetProxyType(IP As String) As String
        Return ProxyQuery(IP, Modes.PROXY_TYPE).Proxy_Type
    End Function

    'Description: Returns a string for the domain
    Public Function GetDomain(IP As String) As String
        Return ProxyQuery(IP, Modes.DOMAIN).Domain
    End Function

    'Description: Returns a string for the usage type
    Public Function GetUsageType(IP As String) As String
        Return ProxyQuery(IP, Modes.USAGE_TYPE).Usage_Type
    End Function

    'Description: Returns a string for the ASN
    Public Function GetASN(IP As String) As String
        Return ProxyQuery(IP, Modes.ASN).ASN
    End Function

    'Description: Returns a string for the AS
    Public Function GetAS(IP As String) As String
        Return ProxyQuery(IP, Modes.AS).AS
    End Function

    'Description: Returns a string for the last seen
    Public Function GetLastSeen(IP As String) As String
        Return ProxyQuery(IP, Modes.LAST_SEEN).Last_Seen
    End Function

    'Description: Returns a string for the threat
    Public Function GetThreat(IP As String) As String
        Return ProxyQuery(IP, Modes.THREAT).Threat
    End Function

    'Description: Returns a string for the provider
    Public Function GetProvider(IP As String) As String
        Return ProxyQuery(IP, Modes.PROVIDER).Provider
    End Function

    'Description: Returns all results
    Public Function GetAll(IP As String) As ProxyResult
        Return ProxyQuery(IP)
    End Function

    ' Description: Read BIN file into memory mapped file and create accessors
    Private Function LoadBIN() As Boolean
        Dim LoadOK As Boolean = False
        Try
            If _DBFilePath <> "" Then
                Using myFileStream As New FileStream(_DBFilePath, FileMode.Open, FileAccess.Read)
                    Dim len = 64 ' 64-byte header
                    Dim row(len - 1) As Byte

                    myFileStream.Seek(0, SeekOrigin.Begin)
                    myFileStream.Read(row, 0, len)

                    _DBType = Read8Header(row, 0)
                    _DBColumn = Read8Header(row, 1)
                    _DBYear = Read8Header(row, 2)
                    _DBMonth = Read8Header(row, 3)
                    _DBDay = Read8Header(row, 4)
                    _DBCount = Read32Header(row, 5) '4 bytes
                    _BaseAddr = Read32Header(row, 9) '4 bytes
                    _DBCountIPv6 = Read32Header(row, 13) '4 bytes
                    _BaseAddrIPv6 = Read32Header(row, 17) '4 bytes
                    _IndexBaseAddr = Read32Header(row, 21) '4 bytes
                    _IndexBaseAddrIPv6 = Read32Header(row, 25) '4 bytes
                    _ProductCode = Read8Header(row, 29)
                    ' below 2 fields just read for now, not being used yet
                    _ProductType = Read8Header(row, 30)
                    _FileSize = Read32Header(row, 31) '4 bytes

                    ' check if is correct BIN (should be 2 for IP2Proxy BIN file), also checking for zipped file (PK being the first 2 chars)
                    If (_ProductCode <> 2 AndAlso _DBYear >= 21) OrElse (_DBType = 80 AndAlso _DBColumn = 75) Then ' only BINs from Jan 2021 onwards have this byte set
                        Throw New Exception(MSG_INVALID_BIN)
                    End If

                    _IPv4ColumnSize = _DBColumn << 2 ' 4 bytes each column
                    _IPv6ColumnSize = 16 + ((_DBColumn - 1) << 2) ' 4 bytes each column, except IPFrom column which is 16 bytes

                    COUNTRY_POSITION_OFFSET = If(COUNTRY_POSITION(_DBType) <> 0, (COUNTRY_POSITION(_DBType) - 2) << 2, 0)
                    REGION_POSITION_OFFSET = If(REGION_POSITION(_DBType) <> 0, (REGION_POSITION(_DBType) - 2) << 2, 0)
                    CITY_POSITION_OFFSET = If(CITY_POSITION(_DBType) <> 0, (CITY_POSITION(_DBType) - 2) << 2, 0)
                    ISP_POSITION_OFFSET = If(ISP_POSITION(_DBType) <> 0, (ISP_POSITION(_DBType) - 2) << 2, 0)
                    PROXYTYPE_POSITION_OFFSET = If(PROXYTYPE_POSITION(_DBType) <> 0, (PROXYTYPE_POSITION(_DBType) - 2) << 2, 0)
                    DOMAIN_POSITION_OFFSET = If(DOMAIN_POSITION(_DBType) <> 0, (DOMAIN_POSITION(_DBType) - 2) << 2, 0)
                    USAGETYPE_POSITION_OFFSET = If(USAGETYPE_POSITION(_DBType) <> 0, (USAGETYPE_POSITION(_DBType) - 2) << 2, 0)
                    ASN_POSITION_OFFSET = If(ASN_POSITION(_DBType) <> 0, (ASN_POSITION(_DBType) - 2) << 2, 0)
                    AS_POSITION_OFFSET = If(AS_POSITION(_DBType) <> 0, (AS_POSITION(_DBType) - 2) << 2, 0)
                    LASTSEEN_POSITION_OFFSET = If(LASTSEEN_POSITION(_DBType) <> 0, (LASTSEEN_POSITION(_DBType) - 2) << 2, 0)
                    THREAT_POSITION_OFFSET = If(THREAT_POSITION(_DBType) <> 0, (THREAT_POSITION(_DBType) - 2) << 2, 0)
                    PROVIDER_POSITION_OFFSET = If(PROVIDER_POSITION(_DBType) <> 0, (PROVIDER_POSITION(_DBType) - 2) << 2, 0)

                    COUNTRY_ENABLED = COUNTRY_POSITION(_DBType) <> 0
                    REGION_ENABLED = REGION_POSITION(_DBType) <> 0
                    CITY_ENABLED = CITY_POSITION(_DBType) <> 0
                    ISP_ENABLED = ISP_POSITION(_DBType) <> 0
                    PROXYTYPE_ENABLED = PROXYTYPE_POSITION(_DBType) <> 0
                    DOMAIN_ENABLED = DOMAIN_POSITION(_DBType) <> 0
                    USAGETYPE_ENABLED = USAGETYPE_POSITION(_DBType) <> 0
                    ASN_ENABLED = ASN_POSITION(_DBType) <> 0
                    AS_ENABLED = AS_POSITION(_DBType) <> 0
                    LASTSEEN_ENABLED = LASTSEEN_POSITION(_DBType) <> 0
                    THREAT_ENABLED = THREAT_POSITION(_DBType) <> 0
                    PROVIDER_ENABLED = PROVIDER_POSITION(_DBType) <> 0

                    Dim readLen = _IndexArrayIPv4.GetLength(0)
                    If _IndexBaseAddrIPv6 > 0 Then
                        readLen += _IndexArrayIPv6.GetLength(0)
                    End If

                    readLen *= 8 ' 4 bytes for both From/To
                    Dim indexData(readLen - 1) As Byte

                    myFileStream.Seek(_IndexBaseAddr - 1, SeekOrigin.Begin)
                    myFileStream.Read(indexData, 0, readLen)

                    Dim pointer As Integer = 0

                    ' read IPv4 index
                    For x As Integer = _IndexArrayIPv4.GetLowerBound(0) To _IndexArrayIPv4.GetUpperBound(0)
                        _IndexArrayIPv4(x, 0) = Read32Header(indexData, pointer) '4 bytes for  row
                        _IndexArrayIPv4(x, 1) = Read32Header(indexData, pointer + 4) '4 bytes for to row
                        pointer += 8
                    Next

                    If _IndexBaseAddrIPv6 > 0 Then
                        ' read IPv6 index
                        For x As Integer = _IndexArrayIPv6.GetLowerBound(0) To _IndexArrayIPv6.GetUpperBound(0)
                            _IndexArrayIPv6(x, 0) = Read32Header(indexData, pointer) '4 bytes for  row
                            _IndexArrayIPv6(x, 1) = Read32Header(indexData, pointer + 4) '4 bytes for to row
                            pointer += 8
                        Next
                    End If
                End Using

                LoadOK = True
            End If
        Catch Ex As Exception
            LogDebug.WriteLog(Ex.Message)
        End Try

        Return LoadOK
    End Function

    ' Description: Reverse the bytes if system is little endian
    Private Sub LittleEndian(ByRef ByteArr() As Byte)
        If BitConverter.IsLittleEndian Then
            Dim ByteList As New List(Of Byte)(ByteArr)
            ByteList.Reverse()
            ByteArr = ByteList.ToArray()
        End If
    End Sub

    ' Description: Initialize the component with the BIN file path and mode
    Public Function Open(ByVal DatabasePath As String, Optional ByVal IOMode As IOModes = IOModes.IP2PROXY_FILE_IO) As Integer
        If _DBType = 0 Then
            _DBFilePath = DatabasePath

            If Not LoadBIN() Then ' problems reading BIN
                Return -1
            Else
                Return 0
            End If
        Else
            Return 0
        End If
    End Function

    ' Description: Query database to get proxy information by IP address
    Private Function ProxyQuery(ByVal IPAddress As String, Optional ByVal Mode As Modes = Modes.ALL) As ProxyResult
        Dim Result As ProxyResult
        Dim StrIP As String
        Dim IPType As Integer = 0
        Dim BaseAddr As Integer = 0
        Dim FS As FileStream = Nothing

        Dim CountryPos As Long = 0
        Dim Low As Long = 0
        Dim High As Long = 0
        Dim Mid As Long = 0
        Dim IPFrom As New IntX()
        Dim IPTo As New IntX()
        Dim IPNum As New IntX()
        Dim IndexAddr As Long = 0
        Dim MAX_IP_RANGE As New IntX()
        Dim RowOffset As Long = 0
        Dim RowOffset2 As Long = 0
        Dim ColumnSize As Integer = 0
        Dim OverCapacity As Boolean = False
        Dim FullRow As Byte() = Nothing
        Dim Row As Byte()
        Dim FirstCol As Integer = 4 ' IP From is 4 bytes

        Try
            If IPAddress = "" OrElse IPAddress Is Nothing Then
                With Result
                    .Is_Proxy = -1
                    .Proxy_Type = MSG_INVALID_IP
                    .Country_Short = MSG_INVALID_IP
                    .Country_Long = MSG_INVALID_IP
                    .Region = MSG_INVALID_IP
                    .City = MSG_INVALID_IP
                    .ISP = MSG_INVALID_IP
                    .Domain = MSG_INVALID_IP
                    .Usage_Type = MSG_INVALID_IP
                    .ASN = MSG_INVALID_IP
                    .AS = MSG_INVALID_IP
                    .Last_Seen = MSG_INVALID_IP
                    .Threat = MSG_INVALID_IP
                    .Provider = MSG_INVALID_IP
                End With
                Return Result
            End If

            StrIP = Me.VerifyIP(IPAddress, IPType, IPNum)

            If StrIP <> "Invalid IP" Then
                IPAddress = StrIP
            Else
                With Result
                    .Is_Proxy = -1
                    .Proxy_Type = MSG_INVALID_IP
                    .Country_Short = MSG_INVALID_IP
                    .Country_Long = MSG_INVALID_IP
                    .Region = MSG_INVALID_IP
                    .City = MSG_INVALID_IP
                    .ISP = MSG_INVALID_IP
                    .Domain = MSG_INVALID_IP
                    .Usage_Type = MSG_INVALID_IP
                    .ASN = MSG_INVALID_IP
                    .AS = MSG_INVALID_IP
                    .Last_Seen = MSG_INVALID_IP
                    .Threat = MSG_INVALID_IP
                    .Provider = MSG_INVALID_IP
                End With
                Return Result
            End If

            ' Read BIN if haven't done so
            If _DBType = 0 Then
                If Not LoadBIN() Then ' problems reading BIN
                    With Result
                        .Is_Proxy = -1
                        .Proxy_Type = MSG_MISSING_FILE
                        .Country_Short = MSG_MISSING_FILE
                        .Country_Long = MSG_MISSING_FILE
                        .Region = MSG_MISSING_FILE
                        .City = MSG_MISSING_FILE
                        .ISP = MSG_MISSING_FILE
                        .Domain = MSG_MISSING_FILE
                        .Usage_Type = MSG_MISSING_FILE
                        .ASN = MSG_MISSING_FILE
                        .AS = MSG_MISSING_FILE
                        .Last_Seen = MSG_MISSING_FILE
                        .Threat = MSG_MISSING_FILE
                        .Provider = MSG_MISSING_FILE
                    End With
                    Return Result
                End If
            End If

            FS = New FileStream(_DBFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)

            Select Case IPType
                Case 4
                    ' IPv4
                    MAX_IP_RANGE = MAX_IPV4_RANGE
                    High = _DBCount
                    BaseAddr = _BaseAddr
                    ColumnSize = _IPv4ColumnSize

                    IndexAddr = IPNum >> 16

                    Low = _IndexArrayIPv4(IndexAddr, 0)
                    High = _IndexArrayIPv4(IndexAddr, 1)
                Case 6
                    ' IPv6
                    FirstCol = 16 ' IPv6 is 16 bytes
                    If _DBCountIPv6 = 0 Then
                        With Result
                            .Is_Proxy = -1
                            .Proxy_Type = MSG_IPV6_UNSUPPORTED
                            .Country_Short = MSG_IPV6_UNSUPPORTED
                            .Country_Long = MSG_IPV6_UNSUPPORTED
                            .Region = MSG_IPV6_UNSUPPORTED
                            .City = MSG_IPV6_UNSUPPORTED
                            .ISP = MSG_IPV6_UNSUPPORTED
                            .Domain = MSG_IPV6_UNSUPPORTED
                            .Usage_Type = MSG_IPV6_UNSUPPORTED
                            .ASN = MSG_IPV6_UNSUPPORTED
                            .AS = MSG_IPV6_UNSUPPORTED
                            .Last_Seen = MSG_IPV6_UNSUPPORTED
                            .Threat = MSG_IPV6_UNSUPPORTED
                            .Provider = MSG_IPV6_UNSUPPORTED
                        End With
                        Return Result
                    End If
                    MAX_IP_RANGE = MAX_IPV6_RANGE
                    High = _DBCountIPv6
                    BaseAddr = _BaseAddrIPv6
                    ColumnSize = _IPv6ColumnSize

                    If _IndexBaseAddrIPv6 > 0 Then
                        IndexAddr = IPNum >> 112
                        Low = _IndexArrayIPv6(IndexAddr, 0)
                        High = _IndexArrayIPv6(IndexAddr, 1)
                    End If
            End Select

            If IPNum >= MAX_IP_RANGE Then
                IPNum = MAX_IP_RANGE - New IntX(1)
            End If

            While (Low <= High)
                Mid = CInt((Low + High) / 2)

                RowOffset = BaseAddr + (Mid * ColumnSize)
                RowOffset2 = RowOffset + ColumnSize

                ' reading IP From + whole row + next IP From
                FullRow = ReadRow(RowOffset, ColumnSize + FirstCol, FS)
                IPFrom = Read32Or128Row(FullRow, 0, FirstCol)
                IPTo = Read32Or128Row(FullRow, ColumnSize, FirstCol)

                If IPNum >= IPFrom AndAlso IPNum < IPTo Then
                    Dim Is_Proxy As Integer = -1
                    Dim Proxy_Type As String = MSG_NOT_SUPPORTED
                    Dim Country_Short As String = MSG_NOT_SUPPORTED
                    Dim Country_Long As String = MSG_NOT_SUPPORTED
                    Dim Region As String = MSG_NOT_SUPPORTED
                    Dim City As String = MSG_NOT_SUPPORTED
                    Dim ISP As String = MSG_NOT_SUPPORTED
                    Dim Domain As String = MSG_NOT_SUPPORTED
                    Dim Usage_Type As String = MSG_NOT_SUPPORTED
                    Dim ASN As String = MSG_NOT_SUPPORTED
                    Dim [AS] As String = MSG_NOT_SUPPORTED
                    Dim Last_Seen As String = MSG_NOT_SUPPORTED
                    Dim Threat As String = MSG_NOT_SUPPORTED
                    Dim Provider As String = MSG_NOT_SUPPORTED

                    Dim RowLen = ColumnSize - FirstCol

                    ReDim Row(RowLen - 1)
                    Array.Copy(FullRow, FirstCol, Row, 0, RowLen) ' extract the actual row data

                    If PROXYTYPE_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.PROXY_TYPE OrElse Mode = Modes.IS_PROXY Then
                            Proxy_Type = ReadStr(Read32Row(Row, PROXYTYPE_POSITION_OFFSET), FS)
                        End If
                    End If
                    If COUNTRY_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.COUNTRY_SHORT OrElse Mode = Modes.COUNTRY_LONG OrElse Mode = Modes.IS_PROXY Then
                            CountryPos = Read32Row(Row, COUNTRY_POSITION_OFFSET)
                        End If
                        If Mode = Modes.ALL OrElse Mode = Modes.COUNTRY_SHORT OrElse Mode = Modes.IS_PROXY Then
                            Country_Short = ReadStr(CountryPos, FS)
                        End If
                        If Mode = Modes.ALL OrElse Mode = Modes.COUNTRY_LONG Then
                            Country_Long = ReadStr(CountryPos + 3, FS)
                        End If
                    End If
                    If REGION_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.REGION Then
                            Region = ReadStr(Read32Row(Row, REGION_POSITION_OFFSET), FS)
                        End If
                    End If
                    If CITY_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.CITY Then
                            City = ReadStr(Read32Row(Row, CITY_POSITION_OFFSET), FS)
                        End If
                    End If
                    If ISP_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.ISP Then
                            ISP = ReadStr(Read32Row(Row, ISP_POSITION_OFFSET), FS)
                        End If
                    End If
                    If DOMAIN_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.DOMAIN Then
                            Domain = ReadStr(Read32Row(Row, DOMAIN_POSITION_OFFSET), FS)
                        End If
                    End If
                    If USAGETYPE_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.USAGE_TYPE Then
                            Usage_Type = ReadStr(Read32Row(Row, USAGETYPE_POSITION_OFFSET), FS)
                        End If
                    End If
                    If ASN_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.ASN Then
                            ASN = ReadStr(Read32Row(Row, ASN_POSITION_OFFSET), FS)
                        End If
                    End If
                    If AS_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.AS Then
                            [AS] = ReadStr(Read32Row(Row, AS_POSITION_OFFSET), FS)
                        End If
                    End If
                    If LASTSEEN_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.LAST_SEEN Then
                            Last_Seen = ReadStr(Read32Row(Row, LASTSEEN_POSITION_OFFSET), FS)
                        End If
                    End If
                    If THREAT_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.THREAT Then
                            Threat = ReadStr(Read32Row(Row, THREAT_POSITION_OFFSET), FS)
                        End If
                    End If
                    If PROVIDER_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.PROVIDER Then
                            Provider = ReadStr(Read32Row(Row, PROVIDER_POSITION_OFFSET), FS)
                        End If
                    End If

                    If Country_Short = "-" OrElse Proxy_Type = "-" Then
                        Is_Proxy = 0
                    Else
                        If Proxy_Type = "DCH" OrElse Proxy_Type = "SES" Then
                            Is_Proxy = 2
                        Else
                            Is_Proxy = 1
                        End If
                    End If

                    With Result
                        .Is_Proxy = Is_Proxy
                        .Proxy_Type = Proxy_Type
                        .Country_Short = Country_Short
                        .Country_Long = Country_Long
                        .Region = Region
                        .City = City
                        .ISP = ISP
                        .Domain = Domain
                        .Usage_Type = Usage_Type
                        .ASN = ASN
                        .AS = [AS]
                        .Last_Seen = Last_Seen
                        .Threat = Threat
                        .Provider = Provider
                    End With
                    Return Result
                Else
                    If IPNum < IPFrom Then
                        High = Mid - 1
                    Else
                        Low = Mid + 1
                    End If
                End If
            End While

            With Result
                .Is_Proxy = -1
                .Proxy_Type = MSG_INVALID_IP
                .Country_Short = MSG_INVALID_IP
                .Country_Long = MSG_INVALID_IP
                .Region = MSG_INVALID_IP
                .City = MSG_INVALID_IP
                .ISP = MSG_INVALID_IP
                .Domain = MSG_INVALID_IP
                .Usage_Type = MSG_INVALID_IP
                .ASN = MSG_INVALID_IP
                .AS = MSG_INVALID_IP
                .Last_Seen = MSG_INVALID_IP
                .Threat = MSG_INVALID_IP
                .Provider = MSG_INVALID_IP
            End With
            Return Result
        Catch ex As Exception
            LogDebug.WriteLog(ex.Message)
            Throw
        Finally
            Result = Nothing
            If FS IsNot Nothing Then
                FS.Close()
                FS.Dispose()
            End If
        End Try
    End Function

    ' Read whole row into array of bytes
    Private Function ReadRow(ByVal _Pos As Long, ByVal MyLen As UInt32, ByRef MyFilestream As FileStream) As Byte()
        Dim row(MyLen - 1) As Byte
        MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
        MyFilestream.Read(row, 0, MyLen)
        Return row
    End Function

    ' Read 8 bits in the binary database
    Private Function Read8(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As Byte
        Dim _Byte(0) As Byte
        MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
        MyFilestream.Read(_Byte, 0, 1)
        Return _Byte(0)
    End Function

    ' Read 8 bits in header
    Private Function Read8Header(ByRef Row() As Byte, ByVal ByteOffset As Integer) As Integer
        Dim _Byte(0) As Byte ' 1 byte
        Array.Copy(Row, ByteOffset, _Byte, 0, 1)
        Return _Byte(0)
    End Function

    ' Read 32 bits in header
    Private Function Read32Header(ByRef Row() As Byte, ByVal ByteOffset As Integer) As Integer
        Dim _Byte(3) As Byte ' 4 bytes
        Array.Copy(Row, ByteOffset, _Byte, 0, 4)
        Return BitConverter.ToUInt32(_Byte, 0)
    End Function

    Private Function Read32Or128Row(ByRef Row() As Byte, ByVal ByteOffset As Integer, ByVal Len As Integer) As IntX
        Dim _Byte(Len - 1) As Byte
        Array.Copy(Row, ByteOffset, _Byte, 0, Len)
        If Len = 4 Then
            Return New IntX(BitConverter.ToUInt32(_Byte, 0).ToString())
        ElseIf Len = 16 Then
            Dim bigRetVal As IntX

            bigRetVal = New IntX(BitConverter.ToUInt64(_Byte, 8).ToString())
            bigRetVal *= SHIFT64BIT
            bigRetVal += New IntX(BitConverter.ToUInt64(_Byte, 0).ToString())

            Return bigRetVal
        Else
            Return New IntX()
        End If
    End Function

    Private Function Read32Or128(ByVal _Pos As Long, ByVal _MyIPType As Integer, ByRef MyFilestream As FileStream) As IntX
        If _MyIPType = 4 Then
            Return Read32(_Pos, MyFilestream)
        ElseIf _MyIPType = 6 Then
            Return Read128(_Pos, MyFilestream)
        Else
            Return New IntX()
        End If
    End Function

    ' Read 128 bits in the binary database
    Private Function Read128(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As IntX
        Dim _Byte(15) As Byte ' 16 bytes
        MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
        MyFilestream.Read(_Byte, 0, 16)

        Dim BigRetVal = New IntX(BitConverter.ToUInt64(_Byte, 8).ToString())
        BigRetVal *= SHIFT64BIT
        BigRetVal += New IntX(BitConverter.ToUInt64(_Byte, 0).ToString())

        Return BigRetVal
    End Function

    ' Read 32 bits in byte array
    Private Function Read32Row(ByRef Row() As Byte, ByVal ByteOffset As Integer) As IntX
        Dim _Byte(3) As Byte ' 4 bytes
        Array.Copy(Row, ByteOffset, _Byte, 0, 4)

        Return New IntX(BitConverter.ToUInt32(_Byte, 0).ToString())
    End Function

    ' Read 32 bits in the binary database
    Private Function Read32(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As IntX
        Dim _Byte(3) As Byte ' 4 bytes
        MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
        MyFilestream.Read(_Byte, 0, 4)

        Return New IntX(BitConverter.ToUInt32(_Byte, 0).ToString())
    End Function

    ' Read strings in the binary database
    Private Function ReadStr(ByVal _Pos As Long, ByRef Myfilestream As FileStream) As String
        Dim _Size = 256 ' max size of string field + 1 byte for the length
        Dim _Data(_Size - 1) As Byte

        Dim _Len As Byte
        Dim _Bytes() As Byte
        Myfilestream.Seek(_Pos, SeekOrigin.Begin)
        Myfilestream.Read(_Data, 0, _Size)
        _Len = _Data(0)
        ReDim _Bytes(_Len - 1)
        Array.Copy(_Data, 1, _Bytes, 0, _Len)
        Return Encoding.Default.GetString(_Bytes)
    End Function

    ' Description: Initialize
    Public Sub New()
    End Sub

    ' Description: Reset results
    Protected Overrides Sub Finalize()
        Close()
        MyBase.Finalize()
    End Sub

    ' Description: Reset results
    Public Function Close() As Integer
        _BaseAddr = 0
        _DBCount = 0
        _DBColumn = 0
        _DBType = 0
        _DBDay = 1
        _DBMonth = 1
        _DBYear = 1
        _BaseAddrIPv6 = 0
        _DBCountIPv6 = 0
        _IndexBaseAddr = 0
        _IndexBaseAddrIPv6 = 0
        _ProductCode = 0
        _ProductType = 0
        _FileSize = 0
        Return 0
    End Function

    ' Description: Validate the IP address input
    Private Function VerifyIP(ByVal StrParam As String, ByRef StrIPType As Integer, ByRef IPNum As IntX) As String
        Try
            Dim Address As IPAddress = Nothing
            Dim FinalIP As String = ""

            'do checks for outlier cases here
            If _OutlierCase1.IsMatch(StrParam) OrElse _OutlierCase2.IsMatch(StrParam) Then 'good ip list outliers
                StrParam = "0000" & StrParam.Substring(1)
            End If

            If Not _OutlierCase3.IsMatch(StrParam) AndAlso Not _OutlierCase4.IsMatch(StrParam) AndAlso Not _OutlierCase5.IsMatch(StrParam) AndAlso IPAddress.TryParse(StrParam, Address) Then
                Select Case Address.AddressFamily
                    Case Sockets.AddressFamily.InterNetwork
                        StrIPType = 4
                    Case Sockets.AddressFamily.InterNetworkV6
                        StrIPType = 6
                    Case Else
                        Return "Invalid IP"
                End Select

                FinalIP = Address.ToString().ToUpper()

                IPNum = IPNo(Address)

                If StrIPType = 6 Then
                    If IPNum >= _FromBI AndAlso IPNum <= _ToBI Then
                        'ipv4-mapped ipv6 should treat as ipv4 and read ipv4 data section
                        StrIPType = 4
                        IPNum -= _FromBI

                        'expand ipv4-mapped ipv6
                        If _IPv4MappedRegex.IsMatch(FinalIP) Then
                            FinalIP = FinalIP.Replace("::", FIVESEGMENTS)
                        ElseIf _IPv4MappedRegex2.IsMatch(FinalIP) Then
                            Dim MyMatch As Match = _IPv4MappedRegex2.Match(FinalIP)
                            Dim x As Integer = 0

                            Dim Tmp As String = MyMatch.Groups(1).ToString()
                            Dim TmpArr() As String = Tmp.Trim(":").Split(":")
                            Dim Len As Integer = TmpArr.Length - 1
                            For x = 0 To Len
                                TmpArr(x) = TmpArr(x).PadLeft(4, "0")
                            Next
                            Dim MyRear As String = String.Join("", TmpArr)
                            Dim Bytes As Byte()

                            Bytes = BitConverter.GetBytes(Convert.ToInt32("0x" & MyRear, 16))
                            FinalIP = FinalIP.Replace(Tmp, ":" & Bytes(3) & "." & Bytes(2) & "." & Bytes(1) & "." & Bytes(0))
                            FinalIP = FinalIP.Replace("::", FIVESEGMENTS)
                        End If
                    ElseIf IPNum >= _FromBI2 AndAlso IPNum <= _ToBI2 Then
                        '6to4 so need to remap to ipv4
                        StrIPType = 4

                        IPNum >>= 80
                        IPNum = IPNum And _DivBI ' get last 32 bits
                    ElseIf IPNum >= _FromBI3 AndAlso IPNum <= _ToBI3 Then
                        'Teredo so need to remap to ipv4
                        StrIPType = 4

                        IPNum = Not IPNum
                        IPNum = IPNum And _DivBI ' get last 32 bits
                    ElseIf IPNum <= MAX_IPV4_RANGE Then
                        'ipv4-compatible ipv6 (DEPRECATED BUT STILL SUPPORTED BY .NET)
                        StrIPType = 4

                        If _IPv4CompatibleRegex.IsMatch(FinalIP) Then
                            Dim Bytes As Byte() = BitConverter.GetBytes(Convert.ToInt32(FinalIP.Replace("::", "0x"), 16))
                            FinalIP = "::" & Bytes(3) & "." & Bytes(2) & "." & Bytes(1) & "." & Bytes(0)
                        ElseIf FinalIP = "::" Then
                            FinalIP &= "0.0.0.0"
                        End If
                        FinalIP = FinalIP.Replace("::", FIVESEGMENTS & "FFFF:")
                    Else
                        'expand ipv6 normal
                        Dim MyArr() As String = Regex.Split(FinalIP, "::")
                        Dim x As Integer = 0
                        Dim LeftSide As New List(Of String)
                        LeftSide.AddRange(MyArr(0).Split(":"))

                        If MyArr.Length > 1 Then
                            Dim RightSide As New List(Of String)
                            RightSide.AddRange(MyArr(1).Split(":"))

                            Dim MidArr As List(Of String)
                            MidArr = Enumerable.Repeat("0000", 8 - LeftSide.Count - RightSide.Count).ToList

                            RightSide.InsertRange(0, MidArr)
                            RightSide.InsertRange(0, LeftSide)

                            Dim RLen As Integer = RightSide.Count - 1
                            For x = 0 To RLen
                                RightSide.Item(x) = RightSide.Item(x).PadLeft(4, "0")
                            Next

                            FinalIP = String.Join(":", RightSide.ToArray())
                        Else
                            Dim LLen As Integer = LeftSide.Count - 1
                            For x = 0 To LLen
                                LeftSide.Item(x) = LeftSide.Item(x).PadLeft(4, "0")
                            Next

                            FinalIP = String.Join(":", LeftSide.ToArray())
                        End If
                    End If

                End If

                Return FinalIP
            Else
                Return "Invalid IP"
            End If
        Catch Ex As Exception
            Return "Invalid IP"
        End Try
    End Function

    ' Description: Convert either IPv4 or IPv6 into big integer
    Private Function IPNo(ByRef IPAddress As IPAddress) As IntX
        Try
            Dim AddrBytes() As Byte = IPAddress.GetAddressBytes()
            LittleEndian(AddrBytes)

            Dim Final As IntX

            If AddrBytes.Length > 8 Then
                'IPv6
                Final = New IntX(System.BitConverter.ToUInt64(AddrBytes, 8).ToString())
                Final *= SHIFT64BIT
                Final += New IntX(System.BitConverter.ToUInt64(AddrBytes, 0).ToString())
            Else
                'IPv4
                Final = New IntX(System.BitConverter.ToUInt32(AddrBytes, 0).ToString())
            End If

            Return Final
        Catch Ex As Exception
            Return New IntX()
        End Try
    End Function

End Class