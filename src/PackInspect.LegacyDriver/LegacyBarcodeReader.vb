''' <summary>
''' Legacy barcode reader driver written in VB.NET.
''' Simulates communication with an industrial fixed-mount barcode scanner.
''' References no other solution projects.
''' </summary>
Public Class LegacyBarcodeReader
    Private _isConnected As Boolean = False
    Private _counter As Integer = 0
    Private ReadOnly _lockObj As New Object()

    Public ReadOnly Property IsConnected As Boolean
        Get
            Return _isConnected
        End Get
    End Property

    Public Sub Connect(portName As String)
        If String.IsNullOrWhiteSpace(portName) Then
            Throw New ArgumentException("Port name cannot be empty.", NameOf(portName))
        End If
        _isConnected = True
    End Sub

    Public Sub Disconnect()
        _isConnected = False
    End Sub

    ''' <summary>
    ''' Triggers a barcode scan for the specified sequence position.
    ''' Returns industrial unit barcode format e.g. "U000017".
    ''' </summary>
    Public Function ReadBarcode(sequenceNumber As Integer) As String
        SyncLock _lockObj
            If Not _isConnected Then
                Throw New InvalidOperationException("Barcode scanner is not connected.")
            End If

            If sequenceNumber < 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(sequenceNumber), "Sequence number must be non-negative.")
            End If

            Return String.Format("U{0:D6}", sequenceNumber)
        End SyncLock
    End Function

    ''' <summary>
    ''' Direct scan returning the next sequential barcode.
    ''' </summary>
    Public Function Scan() As String
        SyncLock _lockObj
            _counter += 1
            Return ReadBarcode(_counter)
        End SyncLock
    End Function
End Class
