﻿'---------------------------------------------------------------------------
' Author       : IP2Location.com
' URL          : http://www.ip2location.com
' Email        : sales@ip2location.com
'
' Copyright (c) 2002-2018 IP2Location.com
'---------------------------------------------------------------------------

Imports System
Imports System.IO
Imports System.Data
Imports System.Net
Imports System.Text
Imports System.Collections
Imports System.Collections.Generic
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
End Structure

Public NotInheritable Class IP2Proxy
    Private _DBFilePath As String = ""
    Private _IndexArrayIPv4(65535, 1) As Integer
    Private _IndexArrayIPv6(65535, 1) As Integer
    Private _OutlierCase1 As Regex = New Regex("^:(:[\dA-F]{1,4}){7}$", RegexOptions.IgnoreCase)
    Private _OutlierCase2 As Regex = New Regex("^:(:[\dA-F]{1,4}){5}:(\d{1,3}\.){3}\d{1,3}$", RegexOptions.IgnoreCase)
    Private _OutlierCase3 As Regex = New Regex("^\d+$")
    Private _OutlierCase4 As Regex = New Regex("^([\dA-F]{1,4}:){6}(0\d+\.|.*?\.0\d+).*$")
    Private _OutlierCase5 As Regex = New Regex("^(\d+\.){1,2}\d+$")
    Private _IPv4MappedRegex As Regex = New Regex("^(.*:)((\d+\.){3}\d+)$")
    Private _IPv4MappedRegex2 As Regex = New Regex("^.*((:[\dA-F]{1,4}){2})$")
    Private _IPv4CompatibleRegex As Regex = New Regex("^::[\dA-F]{1,4}$", RegexOptions.IgnoreCase)
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

    Private _FromBI As New IntX("281470681743360")
    Private _ToBI As New IntX("281474976710655")

    Private Const FIVESEGMENTS As String = "0000:0000:0000:0000:0000:"

    Private SHIFT64BIT As New IntX("18446744073709551616")
    Private MAX_IPV4_RANGE As New IntX("4294967295")
    Private MAX_IPV6_RANGE As New IntX("340282366920938463463374607431768211455")

    Private Const MSG_NOT_SUPPORTED As String = "NOT SUPPORTED"
    Private Const MSG_INVALID_IP As String = "INVALID IP ADDRESS"
    Private Const MSG_MISSING_FILE As String = "MISSING FILE"
    Private Const MSG_IPV6_UNSUPPORTED As String = "IPV6 ADDRESS MISSING IN IPV4 BIN"

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
        ALL = 100
    End Enum

    Private COUNTRY_POSITION() As Byte = {0, 2, 3, 3, 3}
    Private REGION_POSITION() As Byte = {0, 0, 0, 4, 4}
    Private CITY_POSITION() As Byte = {0, 0, 0, 5, 5}
    Private ISP_POSITION() As Byte = {0, 0, 0, 0, 6}
    Private PROXYTYPE_POSITION() As Byte = {0, 0, 2, 2, 2}

    Private COUNTRY_POSITION_OFFSET As Integer = 0
    Private REGION_POSITION_OFFSET As Integer = 0
    Private CITY_POSITION_OFFSET As Integer = 0
    Private ISP_POSITION_OFFSET As Integer = 0
    Private PROXYTYPE_POSITION_OFFSET As Integer = 0

    Private COUNTRY_ENABLED As Boolean = False
    Private REGION_ENABLED As Boolean = False
    Private CITY_ENABLED As Boolean = False
    Private ISP_ENABLED As Boolean = False
    Private PROXYTYPE_ENABLED As Boolean = False

    'Description: Returns the module version
    Public Function GetModuleVersion() As String
        Dim Ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version()
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
        '  1 is proxy except DCH
        '  2 is proxy and DCH
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

    'Description: Returns all results
    Public Function GetAll(IP As String) As ProxyResult
        Return ProxyQuery(IP)
    End Function

    ' Description: Read BIN file into memory mapped file and create accessors
    Private Function LoadBIN() As Boolean
        Dim LoadOK As Boolean = False
        Try
            If _DBFilePath <> "" Then
                Using myFileStream As FileStream = New FileStream(_DBFilePath, FileMode.Open, FileAccess.Read)
                    _DBType = Read8(1, myFileStream)
                    _DBColumn = Read8(2, myFileStream)
                    _DBYear = Read8(3, myFileStream)
                    _DBMonth = Read8(4, myFileStream)
                    _DBDay = Read8(5, myFileStream)
                    _DBCount = Read32(6, myFileStream) '4 bytes
                    _BaseAddr = Read32(10, myFileStream) '4 bytes
                    _DBCountIPv6 = Read32(14, myFileStream) '4 bytes
                    _BaseAddrIPv6 = Read32(18, myFileStream) '4 bytes
                    _IndexBaseAddr = Read32(22, myFileStream) '4 bytes
                    _IndexBaseAddrIPv6 = Read32(26, myFileStream) '4 bytes

                    _IPv4ColumnSize = _DBColumn << 2 ' 4 bytes each column
                    _IPv6ColumnSize = 16 + ((_DBColumn - 1) << 2) ' 4 bytes each column, except IPFrom column which is 16 bytes

                    ' since both IPv4 and IPv6 use 4 bytes for the below columns, can just do it once here
                    COUNTRY_POSITION_OFFSET = If(COUNTRY_POSITION(_DBType) <> 0, (COUNTRY_POSITION(_DBType) - 1) << 2, 0)
                    REGION_POSITION_OFFSET = If(REGION_POSITION(_DBType) <> 0, (REGION_POSITION(_DBType) - 1) << 2, 0)
                    CITY_POSITION_OFFSET = If(CITY_POSITION(_DBType) <> 0, (CITY_POSITION(_DBType) - 1) << 2, 0)
                    ISP_POSITION_OFFSET = If(ISP_POSITION(_DBType) <> 0, (ISP_POSITION(_DBType) - 1) << 2, 0)
                    PROXYTYPE_POSITION_OFFSET = If(PROXYTYPE_POSITION(_DBType) <> 0, (PROXYTYPE_POSITION(_DBType) - 1) << 2, 0)

                    COUNTRY_ENABLED = If(COUNTRY_POSITION(_DBType) <> 0, True, False)
                    REGION_ENABLED = If(REGION_POSITION(_DBType) <> 0, True, False)
                    CITY_ENABLED = If(CITY_POSITION(_DBType) <> 0, True, False)
                    ISP_ENABLED = If(ISP_POSITION(_DBType) <> 0, True, False)
                    PROXYTYPE_ENABLED = If(PROXYTYPE_POSITION(_DBType) <> 0, True, False)

                    Dim Pointer As Integer = _IndexBaseAddr

                    ' read IPv4 index
                    For x As Integer = _IndexArrayIPv4.GetLowerBound(0) To _IndexArrayIPv4.GetUpperBound(0)
                        _IndexArrayIPv4(x, 0) = Read32(Pointer, myFileStream) '4 bytes for from row
                        _IndexArrayIPv4(x, 1) = Read32(Pointer + 4, myFileStream) '4 bytes for to row
                        Pointer += 8
                    Next

                    If _IndexBaseAddrIPv6 > 0 Then
                        ' read IPv6 index
                        For x As Integer = _IndexArrayIPv6.GetLowerBound(0) To _IndexArrayIPv6.GetUpperBound(0)
                            _IndexArrayIPv6(x, 0) = Read32(Pointer, myFileStream) '4 bytes for from row
                            _IndexArrayIPv6(x, 1) = Read32(Pointer + 4, myFileStream) '4 bytes for to row
                            Pointer += 8
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
        If System.BitConverter.IsLittleEndian Then
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
        Dim DBType As Integer = 0
        Dim BaseAddr As Integer = 0
        Dim DBColumn As Integer = 0
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
                    If _DBCountIPv6 = 0 Then
                        With Result
                            .Is_Proxy = -1
                            .Proxy_Type = MSG_IPV6_UNSUPPORTED
                            .Country_Short = MSG_IPV6_UNSUPPORTED
                            .Country_Long = MSG_IPV6_UNSUPPORTED
                            .Region = MSG_IPV6_UNSUPPORTED
                            .City = MSG_IPV6_UNSUPPORTED
                            .ISP = MSG_IPV6_UNSUPPORTED
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

                IPFrom = Read32Or128(RowOffset, IPType, FS)
                IPTo = Read32Or128(RowOffset2, IPType, FS)

                If IPNum >= IPFrom AndAlso IPNum < IPTo Then
                    Dim Is_Proxy As Integer = -1
                    Dim Proxy_Type As String = MSG_NOT_SUPPORTED
                    Dim Country_Short As String = MSG_NOT_SUPPORTED
                    Dim Country_Long As String = MSG_NOT_SUPPORTED
                    Dim Region As String = MSG_NOT_SUPPORTED
                    Dim City As String = MSG_NOT_SUPPORTED
                    Dim ISP As String = MSG_NOT_SUPPORTED

                    If IPType = 6 Then ' IPv6
                        RowOffset = RowOffset + 12 ' coz below is assuming all columns are 4 bytes, so got 12 left to go to make 16 bytes total
                    End If

                    If PROXYTYPE_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.PROXY_TYPE OrElse Mode = Modes.IS_PROXY Then
                            Proxy_Type = ReadStr(Read32(RowOffset + PROXYTYPE_POSITION_OFFSET, FS), FS)
                        End If
                    End If
                    If COUNTRY_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.COUNTRY_SHORT OrElse Mode = Modes.COUNTRY_LONG OrElse Mode = Modes.IS_PROXY Then
                            CountryPos = Read32(RowOffset + COUNTRY_POSITION_OFFSET, FS)
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
                            Region = ReadStr(Read32(RowOffset + REGION_POSITION_OFFSET, FS), FS)
                        End If
                    End If
                    If CITY_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.CITY Then
                            City = ReadStr(Read32(RowOffset + CITY_POSITION_OFFSET, FS), FS)
                        End If
                    End If
                    If ISP_ENABLED Then
                        If Mode = Modes.ALL OrElse Mode = Modes.ISP Then
                            ISP = ReadStr(Read32(RowOffset + ISP_POSITION_OFFSET, FS), FS)
                        End If
                    End If

                    If Country_Short = "-" OrElse Proxy_Type = "-" Then
                        Is_Proxy = 0
                    Else
                        If Proxy_Type = "DCH" Then
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
            End With
            Return Result
        Catch ex As Exception
            LogDebug.WriteLog(ex.Message)
            Throw
        Finally
            Result = Nothing
            If Not FS Is Nothing Then
                FS.Close()
                FS.Dispose()
            End If
        End Try
    End Function

    ' Read 8 bits in the binary database
    Private Function Read8(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As Byte
        Try
            Dim _Byte(0) As Byte
            MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
            MyFilestream.Read(_Byte, 0, 1)
            Return _Byte(0)
        Catch ex As Exception
            'LogDebug.WriteLog("Read8-" & ex.Message)
            Throw
            'Return 0
        End Try
    End Function


    Private Function Read32Or128(ByVal _Pos As Long, ByVal _MyIPType As Integer, ByRef MyFilestream As FileStream) As IntX
        If _MyIPType = 4 Then
            Return Read32(_Pos, MyFilestream)
        ElseIf _MyIPType = 6 Then
            Return Read128(_Pos, MyFilestream) ' only IPv6 will run this
        Else
            Return New IntX()
        End If
    End Function

    ' Read 128 bits in the binary database
    Private Function Read128(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As IntX
        Try
            Dim BigRetVal As New IntX()

            Dim _Byte(15) As Byte ' 16 bytes
            MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
            MyFilestream.Read(_Byte, 0, 16)
            BigRetVal = New IntX(System.BitConverter.ToUInt64(_Byte, 8).ToString())
            BigRetVal *= SHIFT64BIT
            BigRetVal += New IntX(System.BitConverter.ToUInt64(_Byte, 0).ToString())

            Return BigRetVal
        Catch Ex As Exception
            'LogDebug.WriteLog("Read128-" & Ex.Message)
            Throw
            'Return New IntX()
        End Try
    End Function

    ' Read 32 bits in the binary database
    Private Function Read32(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As IntX
        Try
            Dim _Byte(3) As Byte ' 4 bytes
            MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
            MyFilestream.Read(_Byte, 0, 4)

            Return New IntX(System.BitConverter.ToUInt32(_Byte, 0).ToString())
        Catch Ex As Exception
            'LogDebug.WriteLog("Read32-" & Ex.Message)
            Throw
            'Return New IntX()
        End Try
    End Function

    ' Read strings in the binary database
    Private Function ReadStr(ByVal _Pos As Long, ByRef Myfilestream As FileStream) As String
        Try
            Dim _Bytes(0) As Byte
            Dim _Bytes2() As Byte
            Myfilestream.Seek(_Pos, SeekOrigin.Begin)
            Myfilestream.Read(_Bytes, 0, 1)
            ReDim _Bytes2(_Bytes(0) - 1)
            Myfilestream.Read(_Bytes2, 0, _Bytes(0))
            Return System.Text.Encoding.Default.GetString(_Bytes2)
        Catch Ex As Exception
            'LogDebug.WriteLog("ReadStr-" & Ex.Message)
            Throw
            'Return ""
        End Try
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
        Try
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
            Return 0
        Catch Ex As Exception
            Return -1
        End Try
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
                    Case System.Net.Sockets.AddressFamily.InterNetwork
                        ' we have IPv4
                        StrIPType = 4
                        'Return address.ToString()
                    Case System.Net.Sockets.AddressFamily.InterNetworkV6
                        ' we have IPv6
                        StrIPType = 6
                        'Return address.ToString()
                    Case Else
                        Return "Invalid IP"
                End Select

                FinalIP = Address.ToString().ToUpper()

                'get ip number
                IPNum = IPNo(Address)

                If StrIPType = 6 Then
                    If IPNum >= _FromBI AndAlso IPNum <= _ToBI Then
                        'ipv4-mapped ipv6 should treat as ipv4 and read ipv4 data section
                        StrIPType = 4
                        IPNum = IPNum - _FromBI

                        'expand ipv4-mapped ipv6
                        If _IPv4MappedRegex.IsMatch(FinalIP) Then
                            FinalIP = FinalIP.Replace("::", FIVESEGMENTS)
                        ElseIf _IPv4MappedRegex2.IsMatch(FinalIP) Then
                            Dim MyMatch As RegularExpressions.Match = _IPv4MappedRegex2.Match(FinalIP)
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
                    ElseIf IPNum <= MAX_IPV4_RANGE Then
                        'ipv4-compatible ipv6 (DEPRECATED BUT STILL SUPPORTED BY .NET)
                        StrIPType = 4

                        If _IPv4CompatibleRegex.IsMatch(FinalIP) Then
                            Dim Bytes As Byte() = BitConverter.GetBytes(Convert.ToInt32(FinalIP.Replace("::", "0x"), 16))
                            FinalIP = "::" & Bytes(3) & "." & Bytes(2) & "." & Bytes(1) & "." & Bytes(0)
                        ElseIf FinalIP = "::" Then
                            FinalIP = FinalIP & "0.0.0.0"
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