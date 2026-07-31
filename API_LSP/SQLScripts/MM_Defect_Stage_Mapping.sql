-- Defect ↔ Stage (Audit Area) mapping. Many-to-many: one defect can belong to
-- multiple stages. When a stage has any rows here, the defect-entry form shows
-- only those defects for that stage; when it has none, it shows all defects.
-- Idempotent — safe to re-run.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MM_Defect_Stage_Mapping')
BEGIN
    CREATE TABLE [dbo].[MM_Defect_Stage_Mapping](
        [Mapping_ID]       [numeric](18, 0) IDENTITY(1,1) NOT NULL,
        [Audit_Area_ID]    [numeric](18, 0) NOT NULL,
        [Defect_ID]        [numeric](18, 0) NOT NULL,
        [Inserted_Host]    [nvarchar](100) NULL,
        [Inserted_User_ID] [numeric](18, 0) NULL,
        [Inserted_Date]    [datetime] NULL,
        [Plant_Code]       [varchar](15) NULL,
        CONSTRAINT [PK_MM_Defect_Stage_Mapping] PRIMARY KEY CLUSTERED ([Mapping_ID] ASC),
        CONSTRAINT [UQ_MM_Defect_Stage_Mapping] UNIQUE ([Audit_Area_ID], [Defect_ID])
    );

    CREATE INDEX [IX_MM_Defect_Stage_Mapping_Area]
        ON [dbo].[MM_Defect_Stage_Mapping]([Audit_Area_ID]);
END
GO
