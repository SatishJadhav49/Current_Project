USE [PQ_LSP]
GO

/****** Object:  Table [dbo].[MM_4M_Master]    Script Date: 24-07-2026 14:11:01 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[MM_4M_Master](
	[4M_ID] [numeric](18, 0) IDENTITY(1,1) NOT NULL,
	[Plant_ID] [numeric](18, 0) NOT NULL,
	[Category_Name] [nvarchar](100) NULL,
	[Category_Description] [nvarchar](100) NULL,
	[Is_Edited] [bit] NOT NULL,
	[Inserted_Host] [nvarchar](100) NOT NULL,
	[Inserted_User_ID] [numeric](18, 0) NOT NULL,
	[Inserted_Date] [datetime] NOT NULL,
	[Updated_Host] [nvarchar](100) NULL,
	[Updated_User_ID] [numeric](18, 0) NULL,
	[Updated_Date] [datetime] NULL,
	[Is_Transfered] [bit] NOT NULL,
	[Is_Purgeable] [bit] NOT NULL,
	[Plant_Code] [varchar](15) NULL,
 CONSTRAINT [PK__MM_NC_Ca__C4311860A1E4E994] PRIMARY KEY CLUSTERED 
(
	[4M_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[MM_4M_Master] ADD  CONSTRAINT [DF_MM_4M_Master_Is_Transfered]  DEFAULT ((0)) FOR [Is_Transfered]
GO

ALTER TABLE [dbo].[MM_4M_Master] ADD  CONSTRAINT [DF_MM_4M_Master_Is_Purgeable]  DEFAULT ((0)) FOR [Is_Purgeable]
GO

ALTER TABLE [dbo].[MM_4M_Master] ADD  DEFAULT ('A003') FOR [Plant_Code]
GO

ALTER TABLE [dbo].[MM_4M_Master]  WITH CHECK ADD  CONSTRAINT [FK_MM_4M_Master_MM_Plant] FOREIGN KEY([Plant_ID])
REFERENCES [dbo].[MM_Plant] ([Plant_ID])
GO

ALTER TABLE [dbo].[MM_4M_Master] CHECK CONSTRAINT [FK_MM_4M_Master_MM_Plant]
GO


USE [PQ_LSP]
GO

/****** Object:  Table [dbo].[MM_4M_Analysis_Master]    Script Date: 24-07-2026 14:11:32 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[MM_4M_Analysis_Master](
	[4M_Analysis_ID] [numeric](18, 0) IDENTITY(1,1) NOT NULL,
	[4M_ID] [numeric](18, 0) NOT NULL,
	[Plant_ID] [numeric](18, 0) NULL,
	[Analysis_Name] [nvarchar](100) NULL,
	[Analysis_Description] [nvarchar](100) NULL,
	[Is_Deleted] [bit] NULL,
	[Is_Transfered] [bit] NOT NULL,
	[Is_Purgeable] [bit] NOT NULL,
	[Is_Edited] [bit] NULL,
	[Inserted_User_ID] [numeric](18, 0) NOT NULL,
	[Inserted_Date] [datetime] NOT NULL,
	[Updated_Host] [nvarchar](100) NULL,
	[Updated_User_ID] [numeric](18, 0) NULL,
	[Updated_Date] [datetime] NULL,
	[Inserted_Host] [datetime] NOT NULL,
	[Plant_Code] [varchar](15) NULL,
 CONSTRAINT [PK_MM_4M_Analysis_Master] PRIMARY KEY CLUSTERED 
(
	[4M_Analysis_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[MM_4M_Analysis_Master] ADD  DEFAULT ('A003') FOR [Plant_Code]
GO

ALTER TABLE [dbo].[MM_4M_Analysis_Master]  WITH CHECK ADD  CONSTRAINT [FK_MM_4M_Analysis_Master_MM_Plant] FOREIGN KEY([Plant_ID])
REFERENCES [dbo].[MM_Plant] ([Plant_ID])
GO

ALTER TABLE [dbo].[MM_4M_Analysis_Master] CHECK CONSTRAINT [FK_MM_4M_Analysis_Master_MM_Plant]
GO


USE [PQ_LSP]
GO

/****** Object:  Table [dbo].[MM_Audit_Action_Plan_Details]    Script Date: 24-07-2026 14:12:13 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[MM_Audit_Action_Plan_Details](
	[Action_ID] [numeric](18, 0) IDENTITY(1,1) NOT NULL,
	[Vehicle_ID] [numeric](18, 0) NULL,
	[Defect_Entry_ID] [numeric](18, 0) NULL,
	[ICA] [nvarchar](max) NULL,
	[PCA] [nvarchar](max) NULL,
	[Root_Cause] [nvarchar](max) NULL,
	[Target_Date] [datetime] NULL,
	[Status] [nvarchar](100) NULL,
	[Responsibility] [nvarchar](100) NULL,
	[4M_ID] [numeric](18, 0) NULL,
	[4M_Analysis_ID] [numeric](18, 0) NULL,
	[Department] [nvarchar](50) NULL,
	[Stage_ID] [numeric](18, 0) NOT NULL,
	[CutOff] [nvarchar](50) NULL,
	[Status_G_Date] [datetime] NULL,
	[Is_HOD_Approved] [bit] NULL,
	[Approved_User_Id] [numeric](18, 0) NULL,
	[Approved_Date] [datetime] NULL,
	[Audit_Type_Id] [numeric](18, 0) NULL,
	[Is_Transfered] [bit] NULL,
	[Is_Purgeable] [bit] NULL,
	[Is_Edited] [bit] NULL,
	[Is_Deleted] [bit] NULL,
	[Inserted_Host] [nvarchar](100) NULL,
	[Inserted_User_ID] [numeric](18, 0) NULL,
	[Inserted_Date] [datetime] NULL,
	[Updated_Host] [nvarchar](100) NULL,
	[Updated_User_ID] [numeric](18, 0) NULL,
	[Updated_Date] [datetime] NULL,
	[Plant_Code] [varchar](15) NULL
 CONSTRAINT [PK_MM_Audit_Action_Plan_Details] PRIMARY KEY CLUSTERED 
(
	[Action_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[MM_Audit_Action_Plan_Details] ADD  CONSTRAINT [DF__MM_Audit___Plant__0A338187]  DEFAULT ('A003') FOR [Plant_Code]
GO
