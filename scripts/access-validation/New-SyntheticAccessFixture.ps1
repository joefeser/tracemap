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
        FormReportCoverage = "deferred-except-startup-canary"
        VbaCoverage = "deferred-except-startup-canary"
        MacroCoverage = "deferred"
    } | ConvertTo-Json -Compress
}
finally {
    Close-ComObject $startupForm
    Close-ComObject $passThrough
    Close-ComObject $linkedTable
    Close-ComObject $database
    if ($null -ne $access) { try { $access.Quit(2) } catch { } }
    Close-ComObject $access
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}
