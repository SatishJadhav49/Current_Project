-- Action Plan (concern resolution tracking). A plan is keyed on a Problem
-- (Part + Defect) per Attribution + Severity. Header + 3 child tables so each
-- section (Root Cause / ICA / PCA) can hold multiple rows. Idempotent.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MM_Action_Plan')
BEGIN
    CREATE TABLE [dbo].[MM_Action_Plan](
        [Action_Plan_ID]   [numeric](18, 0) IDENTITY(1,1) NOT NULL,
        [Model_ID]         [numeric](18, 0) NOT NULL,
        [Part_ID]          [numeric](18, 0) NOT NULL,
        [Defect_ID]        [numeric](18, 0) NOT NULL,
        [Attribution_ID]   [numeric](18, 0) NOT NULL,
        [Severity_ID]      [numeric](18, 0) NOT NULL,   -- 0 = no severity
        [Is_Submitted]     [bit] NOT NULL CONSTRAINT [DF_MM_Action_Plan_Is_Submitted] DEFAULT ((0)),
        [Inserted_Host]    [nvarchar](100) NULL,
        [Inserted_User_ID] [numeric](18, 0) NULL,
        [Inserted_Date]    [datetime] NULL,
        [Plant_Code]       [varchar](15) NULL,
        CONSTRAINT [PK_MM_Action_Plan] PRIMARY KEY CLUSTERED ([Action_Plan_ID] ASC),
        CONSTRAINT [UQ_MM_Action_Plan] UNIQUE ([Model_ID], [Part_ID], [Defect_ID], [Attribution_ID], [Severity_ID])
    );
END
GO

-- Migration: add Model_ID to an existing table + re-key the unique constraint.
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MM_Action_Plan')
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('MM_Action_Plan') AND name = 'Model_ID')
BEGIN
    ALTER TABLE [dbo].[MM_Action_Plan] ADD [Model_ID] [numeric](18, 0) NOT NULL CONSTRAINT [DF_MM_Action_Plan_Model_ID] DEFAULT ((0));

    IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'UQ_MM_Action_Plan' AND type = 'UQ')
        ALTER TABLE [dbo].[MM_Action_Plan] DROP CONSTRAINT [UQ_MM_Action_Plan];

    ALTER TABLE [dbo].[MM_Action_Plan]
        ADD CONSTRAINT [UQ_MM_Action_Plan] UNIQUE ([Model_ID], [Part_ID], [Defect_ID], [Attribution_ID], [Severity_ID]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MM_Action_Plan_Root_Cause')
BEGIN
    CREATE TABLE [dbo].[MM_Action_Plan_Root_Cause](
        [Root_Cause_ID]    [numeric](18, 0) IDENTITY(1,1) NOT NULL,
        [Action_Plan_ID]   [numeric](18, 0) NOT NULL,
        [Root_Cause]       [nvarchar](1000) NULL,
        [Inserted_Host]    [nvarchar](100) NULL,
        [Inserted_User_ID] [numeric](18, 0) NULL,
        [Inserted_Date]    [datetime] NULL,
        CONSTRAINT [PK_MM_Action_Plan_Root_Cause] PRIMARY KEY CLUSTERED ([Root_Cause_ID] ASC)
    );
    CREATE INDEX [IX_MM_AP_Root_Cause_Plan] ON [dbo].[MM_Action_Plan_Root_Cause]([Action_Plan_ID]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MM_Action_Plan_ICA')
BEGIN
    CREATE TABLE [dbo].[MM_Action_Plan_ICA](
        [ICA_ID]           [numeric](18, 0) IDENTITY(1,1) NOT NULL,
        [Action_Plan_ID]   [numeric](18, 0) NOT NULL,
        [Action]           [nvarchar](1000) NULL,
        [Responsibility]   [nvarchar](200) NULL,
        [ICA_Date]         [datetime] NULL,
        [Inserted_Host]    [nvarchar](100) NULL,
        [Inserted_User_ID] [numeric](18, 0) NULL,
        [Inserted_Date]    [datetime] NULL,
        CONSTRAINT [PK_MM_Action_Plan_ICA] PRIMARY KEY CLUSTERED ([ICA_ID] ASC)
    );
    CREATE INDEX [IX_MM_AP_ICA_Plan] ON [dbo].[MM_Action_Plan_ICA]([Action_Plan_ID]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MM_Action_Plan_PCA')
BEGIN
    CREATE TABLE [dbo].[MM_Action_Plan_PCA](
        [PCA_ID]           [numeric](18, 0) IDENTITY(1,1) NOT NULL,
        [Action_Plan_ID]   [numeric](18, 0) NOT NULL,
        [Action]           [nvarchar](1000) NULL,
        [Responsibility]   [nvarchar](200) NULL,
        [Target_Date]      [datetime] NULL,
        [Status]           [char](1) NOT NULL CONSTRAINT [DF_MM_Action_Plan_PCA_Status] DEFAULT ('R'),
        [Inserted_Host]    [nvarchar](100) NULL,
        [Inserted_User_ID] [numeric](18, 0) NULL,
        [Inserted_Date]    [datetime] NULL,
        CONSTRAINT [PK_MM_Action_Plan_PCA] PRIMARY KEY CLUSTERED ([PCA_ID] ASC)
    );
    CREATE INDEX [IX_MM_AP_PCA_Plan] ON [dbo].[MM_Action_Plan_PCA]([Action_Plan_ID]);
END
GO

-- Supporting index for the Action Plan aggregations over reported concerns
-- (frequency + recurrence group-bys on MM_Vehicle_Defects).
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_MM_Vehicle_Defects_Problem'
                 AND object_id = OBJECT_ID('MM_Vehicle_Defects'))
BEGIN
    CREATE INDEX [IX_MM_Vehicle_Defects_Problem]
        ON [dbo].[MM_Vehicle_Defects]([Part_ID], [Defect_ID], [Attribution_ID], [Severity_ID])
        INCLUDE ([Vehicle_ID], [Inserted_Date]);
END
GO
