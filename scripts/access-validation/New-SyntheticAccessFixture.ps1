param(
    [Parameter(Mandatory = $true)]
    [string]$DatabasePath,

    [Parameter(Mandatory = $true)]
    [string]$CanaryPath
)

$ErrorActionPreference = "Stop"
$DatabasePath = [IO.Path]::GetFullPath($DatabasePath)
$CanaryPath = [IO.Path]::GetFullPath($CanaryPath)
$fixtureDirectory = Split-Path -Parent $DatabasePath
$externalPath = Join-Path $fixtureDirectory "PrivateWarehouse_92817.accdb"
$mdbPath = [IO.Path]::ChangeExtension($DatabasePath, ".mdb")
New-Item -ItemType Directory -Path $fixtureDirectory -Force | Out-Null
Remove-Item $DatabasePath, $externalPath, $mdbPath, $CanaryPath -Force -ErrorAction SilentlyContinue

function Close-ComObject([object]$Value) {
    if ($null -ne $Value) {
        try { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($Value) } catch { }
    }
}

function New-ExternalFixture([string]$Path) {
    $app = $null
    $db = $null
    try {
        $app = New-Object -ComObject Access.Application
        $app.AutomationSecurity = 3
        $app.Visible = $false
        $app.NewCurrentDatabase($Path)
        $db = $app.CurrentDb()
        $db.Execute("CREATE TABLE WarehouseStatus (WarehouseId LONG NOT NULL, StatusCode TEXT(24))", 128)
        $app.CloseCurrentDatabase()
    }
    finally {
        Close-ComObject $db
        if ($null -ne $app) { try { $app.Quit(2) } catch { } }
        Close-ComObject $app
    }
}

New-ExternalFixture $externalPath

$mdbCreated = $false
$mdbAccess = $null
$mdbDatabase = $null
try {
    $mdbAccess = New-Object -ComObject Access.Application
    $mdbAccess.AutomationSecurity = 3
    $mdbAccess.Visible = $false
    $mdbAccess.NewCurrentDatabase($mdbPath, 10) # Access 2002-2003 .mdb format
    $mdbDatabase = $mdbAccess.CurrentDb()
    $mdbDatabase.Execute("CREATE TABLE MdbCatalogCanary (CatalogId LONG NOT NULL, Label TEXT(32))", 128)
    $mdbAccess.CloseCurrentDatabase()
    $mdbCreated = $true
}
catch {
    Remove-Item $mdbPath -Force -ErrorAction SilentlyContinue
}
finally {
    Close-ComObject $mdbDatabase
    if ($null -ne $mdbAccess) { try { $mdbAccess.Quit(2) } catch { } }
    Close-ComObject $mdbAccess
}

$access = $null
$database = $null
$linkedTable = $null
$passThrough = $null
$startupForm = $null
$customerForm = $null
$customerFormModule = $null
$ordersReport = $null
$fixtureControls = [System.Collections.Generic.List[object]]::new()
try {
    $access = New-Object -ComObject Access.Application
    $access.AutomationSecurity = 3
    $access.Visible = $false
    $access.NewCurrentDatabase($DatabasePath)
    $database = $access.CurrentDb()
    $dbFailOnError = 128

    $database.Execute(@"
CREATE TABLE Customers (
    CustomerId AUTOINCREMENT CONSTRAINT PK_Customers PRIMARY KEY,
    DisplayName TEXT(120) NOT NULL,
    [Customer Note] TEXT(50),
    IsActive YESNO NOT NULL,
    CreatedAt DATETIME
)
"@, $dbFailOnError)
    $database.Execute(@"
CREATE TABLE Orders (
    OrderId AUTOINCREMENT CONSTRAINT PK_Orders PRIMARY KEY,
    CustomerId LONG NOT NULL,
    OrderStatus TEXT(30) NOT NULL,
    OrderedAt DATETIME,
    TotalAmount CURRENCY
)
"@, $dbFailOnError)
    $database.Execute(@"
CREATE TABLE OrderItems (
    OrderItemId AUTOINCREMENT CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderId LONG NOT NULL,
    ProductCode TEXT(40) NOT NULL,
    Quantity LONG NOT NULL,
    UnitPrice CURRENCY
)
"@, $dbFailOnError)
    $database.Execute(@"
CREATE TABLE AuditLog (
    AuditLogId AUTOINCREMENT CONSTRAINT PK_AuditLog PRIMARY KEY,
    EventKind TEXT(50) NOT NULL,
    EntityId LONG,
    RecordedAt DATETIME
)
"@, $dbFailOnError)
    $database.Execute("CREATE INDEX IX_Orders_CustomerId ON Orders (CustomerId)", $dbFailOnError)
    $database.Execute("CREATE INDEX IX_OrderItems_OrderId ON OrderItems (OrderId)", $dbFailOnError)
    $database.Execute("CREATE INDEX IX_Customers_CustomerNote ON Customers ([Customer Note])", $dbFailOnError)
    $database.Execute("ALTER TABLE Orders ADD CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES Customers (CustomerId)", $dbFailOnError)
    $database.Execute("ALTER TABLE OrderItems ADD CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES Orders (OrderId)", $dbFailOnError)

    [void]$database.CreateQueryDef("qryCustomerOrderSummary", @"
SELECT Customers.CustomerId, Customers.DisplayName, Count(Orders.OrderId) AS OrderCount
FROM Customers LEFT JOIN Orders ON Customers.CustomerId = Orders.CustomerId
GROUP BY Customers.CustomerId, Customers.DisplayName;
"@)
    [void]$database.CreateQueryDef("qryOrdersByStatus", @"
PARAMETERS [RequestedStatus] Text (30);
SELECT Orders.OrderId, Orders.CustomerId, Orders.OrderStatus
FROM Orders WHERE Orders.OrderStatus = [RequestedStatus];
"@)
    [void]$database.CreateQueryDef("qryAppendAuditCanary_92817", @"
INSERT INTO AuditLog (EventKind, EntityId, RecordedAt)
SELECT 'ACTION_QUERY_CANARY_92817', Orders.OrderId, Now() FROM Orders;
"@)

    $passThrough = $database.CreateQueryDef("qryPrivateWarehouse_92817")
    $passThrough.Connect = "ODBC;DRIVER={PostgreSQL Unicode};SERVER=private-sql-92817.invalid;DATABASE=warehouse;UID=fixture_user;PWD=FixturePassword_92817;"
    $passThrough.ReturnsRecords = $true
    $passThrough.SQL = "SELECT PRIVATE_SQL_MARKER_92817 FROM restricted_warehouse"
    $passThrough.Close()
    Close-ComObject $passThrough
    $passThrough = $null

    $linkedTable = $database.CreateTableDef("lnkPrivateWarehouse_92817")
    $linkedTable.Connect = ";DATABASE=$externalPath"
    $linkedTable.SourceTableName = "WarehouseStatus"
    $database.TableDefs.Append($linkedTable)
    Close-ComObject $linkedTable
    $linkedTable = $null

    # This form exists only as a startup non-execution canary. Phase 7 will add
    # representative surface/control fixtures and extraction assertions.
    $startupForm = $access.CreateForm()
    $temporaryName = $startupForm.Name
    $startupForm.HasModule = $true
    $startupForm.OnOpen = "[Event Procedure]"
    $escapedCanaryPath = $CanaryPath.Replace('"', '""')
    $startupForm.Module.InsertLines(1, @"
Option Compare Database
Option Explicit
Private Sub Form_Open(Cancel As Integer)
    Dim handle As Integer
    handle = FreeFile
    Open "$escapedCanaryPath" For Output As #handle
    Print #handle, "STARTUP_CANARY_FIRED_92817"
    Close #handle
End Sub
"@)
    $access.DoCmd.Save(2, $temporaryName)
    $access.DoCmd.Close(2, $temporaryName, 1)
    $access.DoCmd.Rename("frmStartupCanary_92817", 2, $temporaryName)
    Close-ComObject $startupForm
    $startupForm = $null

    # Phase 7 disposable design fixture. Generator-only CreateForm/CreateReport
    # calls are allowed here; the scanner must never open or render these saved
    # surfaces. Protected values make accidental export immediately observable.
    $customerForm = $access.CreateForm()
    $customerFormTemporaryName = [string]$customerForm.Name
    $customerForm.RecordSource = "Customers"
    $customerForm.HasModule = $true
    $customerName = $access.CreateControl($customerFormTemporaryName, 109, 0, "", "", 1800, 400, 2400, 300)
    $fixtureControls.Add($customerName)
    $customerName.Name = "txtCustomerName"
    $customerName.ControlSource = "DisplayName"
    $customerNameLabel = $access.CreateControl($customerFormTemporaryName, 100, 0, "", "", 200, 400, 1400, 300)
    $fixtureControls.Add($customerNameLabel)
    $customerNameLabel.Name = "lblCustomerName"
    $customerNameLabel.Caption = "FORM_CAPTION_MARKER_92817"
    $orderSelector = $access.CreateControl($customerFormTemporaryName, 111, 0, "", "", 1800, 900, 2400, 300)
    $fixtureControls.Add($orderSelector)
    $orderSelector.Name = "cboOrderStatus"
    $orderSelector.RowSourceType = "Table/Query"
    $orderSelector.RowSource = "qryOrdersByStatus"
    $calculatedControl = $access.CreateControl($customerFormTemporaryName, 109, 0, "", "", 1800, 1400, 2400, 300)
    $fixtureControls.Add($calculatedControl)
    $calculatedControl.Name = "txtCalculatedCustomer"
    $calculatedControl.ControlSource = '=[CustomerId] & "FORM_EXPRESSION_MARKER_92817"'
    $probeButton = $access.CreateControl($customerFormTemporaryName, 104, 0, "", "", 1800, 1900, 2400, 400)
    $fixtureControls.Add($probeButton)
    $probeButton.Name = "cmdPhase7Canary"
    $probeButton.Caption = "FORM_BUTTON_MARKER_92817"
    $escapedPhase7CanaryPath = $CanaryPath.Replace('"', '""')
    $probeButton.OnClick = "=Shell(`"cmd.exe /c echo PHASE7_EVENT_CANARY_FIRED_92817 > `"`"$escapedPhase7CanaryPath`"`"`",0)"

    # Phase 8 generator-only code-behind fixture. The scanner must never open,
    # compile, invoke, export, or persist this source. The first event statement
    # writes the shared sentinel so any accidental invocation is observable.
    $vbaFlowButton = $access.CreateControl($customerFormTemporaryName, 104, 0, "", "", 1800, 2400, 2400, 400)
    $fixtureControls.Add($vbaFlowButton)
    $vbaFlowButton.Name = "cmdVbaFlow"
    $vbaFlowButton.Caption = "VBA_BUTTON_MARKER_92817"
    $vbaFlowButton.OnClick = "[Event Procedure]"
    $escapedPhase8CanaryPath = $CanaryPath.Replace('"', '""')
    $customerFormModule = $customerForm.Module
    $customerFormModule.InsertLines(1, @"
Option Compare Database
Option Explicit
' VBA_COMMENT_MARKER_92817
Private Sub cmdVbaFlow_Click()
    Dim handle As Integer
    Dim target As String
    Dim q As Object
    Dim rs As Object
    Dim value As Variant
    handle = FreeFile
    Open "$escapedPhase8CanaryPath" For Output As #handle
    Print #handle, "PHASE8_VBA_CANARY_FIRED_92817"
    Close #handle
    Call HelperStatic
    DoCmd.OpenForm "frmCustomers"
    DoCmd.OpenReport "rptOrders"
    DoCmd.OpenQuery "qryOrdersByStatus"
    Set q = CurrentDb.QueryDefs("qryOrdersByStatus")
    Set rs = CurrentDb.OpenRecordset("Customers")
    value = DLookup("CustomerId", "Customers")
    target = "frmCustomers"
    DoCmd.OpenForm target
    value = "VBA_LITERAL_MARKER_92817"
    value = "SELECT * FROM VBA_SQL_MARKER_92817"
    value = "C:\Private\VBA_PATH_MARKER_92817.txt"
    Eval ("VBA_EVAL_MARKER_92817")
    Application.Run "VBA_RUN_MARKER_92817"
    CreateObject("WScript.Shell").Run "VBA_COMMAND_MARKER_92817"
End Sub

Private Sub HelperStatic()
End Sub
"@)
    $access.DoCmd.Save(2, $customerFormTemporaryName)
    $access.DoCmd.Close(2, $customerFormTemporaryName, 1)
    $access.DoCmd.Rename("frmCustomers", 2, $customerFormTemporaryName)
    Close-ComObject $customerFormModule
    $customerFormModule = $null
    Close-ComObject $customerForm
    $customerForm = $null
    foreach ($control in $fixtureControls) { Close-ComObject $control }
    $fixtureControls.Clear()

    $ordersReport = $access.CreateReport()
    $ordersReportTemporaryName = [string]$ordersReport.Name
    $ordersReport.RecordSource = "Orders"
    $ordersReport.HasModule = $false
    $statusControl = $access.CreateReportControl($ordersReportTemporaryName, 109, 0, "", "", 1800, 400, 2400, 300)
    $fixtureControls.Add($statusControl)
    $statusControl.Name = "txtOrderStatus"
    $statusControl.ControlSource = "OrderStatus"
    $statusLabel = $access.CreateReportControl($ordersReportTemporaryName, 100, 0, "", "", 200, 400, 1400, 300)
    $fixtureControls.Add($statusLabel)
    $statusLabel.Name = "lblOrderStatus"
    $statusLabel.Caption = "REPORT_CAPTION_MARKER_92817"
    $reportCalculated = $access.CreateReportControl($ordersReportTemporaryName, 109, 0, "", "", 1800, 900, 2400, 300)
    $fixtureControls.Add($reportCalculated)
    $reportCalculated.Name = "txtCalculatedOrder"
    $reportCalculated.ControlSource = '=[OrderId] & "REPORT_EXPRESSION_MARKER_92817"'
    $access.DoCmd.Save(3, $ordersReportTemporaryName)
    $access.DoCmd.Close(3, $ordersReportTemporaryName, 1)
    $access.DoCmd.Rename("rptOrders", 3, $ordersReportTemporaryName)
    Close-ComObject $ordersReport
    $ordersReport = $null
    foreach ($control in $fixtureControls) { Close-ComObject $control }
    $fixtureControls.Clear()

    try {
        $database.Properties.Item("StartupForm").Value = "frmStartupCanary_92817"
    }
    catch {
        $property = $database.CreateProperty("StartupForm", 10, "frmStartupCanary_92817")
        $database.Properties.Append($property)
        Close-ComObject $property
    }

    $access.CloseCurrentDatabase()
    [pscustomobject]@{
        Schema = "tracemap.access-synthetic-fixture.v1"
        DatabasePath = $DatabasePath
        ExternalPath = $externalPath
        MdbPath = if ($mdbCreated) { $mdbPath } else { $null }
        CanaryPath = $CanaryPath
        LocalTables = 4
        Relationships = 2
        SavedQueries = 4
        LinkedTables = 1
        MdbCreated = $mdbCreated
        RowsInserted = 0
        StartupCanary = "configured"
        Forms = 2
        Reports = 1
        Phase7Controls = 8
        Phase8Controls = 1
        TotalFormReportControls = 9
        FormReportCoverage = "phase7-design-fixture"
        VbaCoverage = "phase8-form-code-behind-fixture"
        MacroCoverage = "deferred"
    } | ConvertTo-Json -Compress
}
finally {
    foreach ($control in $fixtureControls) { Close-ComObject $control }
    Close-ComObject $ordersReport
    Close-ComObject $customerFormModule
    Close-ComObject $customerForm
    Close-ComObject $startupForm
    Close-ComObject $passThrough
    Close-ComObject $linkedTable
    Close-ComObject $database
    if ($null -ne $access) { try { $access.Quit(2) } catch { } }
    Close-ComObject $access
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}
