using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using CrystalDecisions.Shared;
using CrystalDecisions.Web;
using CrystalDecisions.CrystalReports.Engine;
using System.Reflection;
using System.Data.SqlClient;

namespace WebApplication1.App_Start
{
    /// <summary>
    /// Report type for show
    /// </summary>
    public enum ReportType
    {
        ToPDFFile,
        ToExcel,
        ToWord,
        InlineView
    }

    public class ReportSource
    {
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static ReportSource Instance { get { return SingletonInstance; } }
        private static readonly ReportSource SingletonInstance = new ReportSource();

        public DataTable ReportData { get; set; }
        public ReportDocument ReportDocumentObj { get; set; }

        public void ReleaseReportData()
        {
            this.ReportData = null;
        }
    }

    public class ReportHandler
    {
        /// <summary>
        /// We can dynamic change the report provider by this function
        /// </summary>
        /// <param name="serverName"></param>
        /// <param name="databaseName"></param>
        /// <param name="userID"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private static TableLogOnInfo GetSQLTableLogOnInfo(string serverName, string databaseName, string userID, string password)
        {
            CrystalDecisions.ReportAppServer.DataDefModel.PropertyBag connectionAttributes = new CrystalDecisions.ReportAppServer.DataDefModel.PropertyBag();
            connectionAttributes.EnsureCapacity(11);
            connectionAttributes.Add("Connect Timeout", "15");
            connectionAttributes.Add("Data Source", serverName);
            connectionAttributes.Add("General Timeout", "0");
            connectionAttributes.Add("Initial Catalog", databaseName);
            connectionAttributes.Add("Integrated Security", false);
            connectionAttributes.Add("Locale Identifier", "1033");
            connectionAttributes.Add("OLE DB Services", "-5");
            //connectionAttributes.Add("Provider", "SQLNCLI11");
            connectionAttributes.Add("Provider", "SQLOLEDB");
            connectionAttributes.Add("Tag with column collation when possible", "0");
            connectionAttributes.Add("Use DSN Default Properties", false);
            connectionAttributes.Add("Use Encryption for Data", "0");

            connectionAttributes.Add("Trust Server Certificate", 0);
            connectionAttributes.Add("DataTypeCompatibility", 0);
            connectionAttributes.Add("Auto Translate", -1);
            connectionAttributes.Add("Application Intent", "READWRITE");

            DbConnectionAttributes attributes = new DbConnectionAttributes();
            attributes.Collection.Add(new NameValuePair2("Database DLL", "crdb_ado.dll"));
            attributes.Collection.Add(new NameValuePair2("QE_DatabaseName", databaseName));
            attributes.Collection.Add(new NameValuePair2("QE_DatabaseType", "OLE DB (ADO)"));
            attributes.Collection.Add(new NameValuePair2("QE_LogonProperties", connectionAttributes));
            attributes.Collection.Add(new NameValuePair2("QE_ServerDescription", serverName));
            attributes.Collection.Add(new NameValuePair2("SSO Enabled", false));
            attributes.Collection.Add(new NameValuePair2("QE_SQLDB", true));
            attributes.Collection.Add(new NameValuePair2("Owner", "dbo"));


            CrystalDecisions.Shared.ConnectionInfo connectionInfo = new CrystalDecisions.Shared.ConnectionInfo();
            connectionInfo.Attributes = attributes;
            connectionInfo.ServerName = serverName;
            connectionInfo.UserID = userID;
            connectionInfo.Password = password;
            connectionInfo.DatabaseName = databaseName;
            connectionInfo.Type = ConnectionInfoType.SQL;

            TableLogOnInfo tableLogOnInfo = new TableLogOnInfo();
            tableLogOnInfo.ConnectionInfo = connectionInfo;
            return tableLogOnInfo;
        }

        private static string getCommandText(ReportDocument rd)
        {
            if (!rd.IsLoaded)
            {
                //throw new ArgumentException("Please ensure that the reportDocument has been loaded before being passed to getCommandText");
                return "";
            }

            PropertyInfo pi = rd.Database.Tables.GetType().GetProperty("RasTables", BindingFlags.NonPublic | BindingFlags.Instance);
            return ((dynamic)pi.GetValue(rd.Database.Tables, pi.GetIndexParameters()))[0].CommandText;
        }

        /// <summary>
        /// Print report 
        /// </summary>
        /// <param name="reportViewer">report viewer</param>
        /// <param name="reportFile">.rpt report file name and full path</param>
        /// <param name="paramList">report's parameters, string format like @name = value</param>
        /// <param name="dataSource">the data source for NOT sp report</param>
        /// <param name="printType">print report type</param>
        /// <param name="fileName">save as report file name</param>
        /// <param name="response">response object for save report to file</param>
        /// <param name="isStoredProcedureReport">whether is use stored procedure report</param>
        private static void PrintReport(CrystalReportViewer reportViewer, string reportName, string reportFile, string paramList, DataTable dataSource, ReportType printType,
            string fileName, HttpResponse response, bool isStoredProcedureReport = true)
        {

            reportViewer.PrintMode = PrintMode.Pdf;
            reportViewer.HasToggleGroupTreeButton = false;
            reportViewer.HasExportButton = false;
            reportViewer.HasToggleParameterPanelButton = false;

            var objReport = new ReportDocument();
            objReport.Load(reportFile);


            var paraValue = new ParameterDiscreteValue();
            var currValue = new ParameterValues();

            //todo: get the database current connection string, you can get from web.config
            string connectionString = "";
            var reportTable = ""; //for generate excel

            if (!string.IsNullOrEmpty(connectionString))
            {
                SqlConnectionStringBuilder sConn = new SqlConnectionStringBuilder(connectionString);

                TableLogOnInfo tableLogOnInfo = GetSQLTableLogOnInfo(sConn.DataSource, sConn.InitialCatalog, sConn.UserID, sConn.Password);
                

                if (isStoredProcedureReport)
                {
                    //apply the connection into report
                    foreach (CrystalDecisions.CrystalReports.Engine.Table table in objReport.Database.Tables)
                    {
                        table.ApplyLogOnInfo(tableLogOnInfo);

                        reportTable = table.Location;

                        //table.Location = "dbo." + table.Location;
                    }
                }

                //apply connection to all sub reports
                foreach (ReportDocument subReport in objReport.Subreports)
                {
                    //apply the connection into report
                    foreach (CrystalDecisions.CrystalReports.Engine.Table table in subReport.Database.Tables)
                    {
                        table.ApplyLogOnInfo(tableLogOnInfo);

                        //table.Location = "dbo." + table.Location;  //the table or sp name in report
                    }
                }
            }

            if (!isStoredProcedureReport)
            {
                objReport.SetDataSource(dataSource);
            }

            var paramCounter = objReport.DataDefinition.ParameterFields.Count;
            //apply the params to report SP
            if (paramCounter > 0 && !string.IsNullOrEmpty(paramList))
            {
                var strParValPair = paramList.Split(';');
                var isCommand = reportTable.Replace(";1", "").ToLower().Contains("command");

                foreach (var item in strParValPair)
                {
                    if (item.IndexOf('=') > 0)
                    {
                        var param = item.Split('=');

                        if (param[1].ToUpper() == "" || param[1].ToUpper() == "NULL")
                        {
                            paraValue.Value = System.DBNull.Value;
                        }
                        else
                        {
                            paraValue.Value = param[1].Trim();
                        }

                        var paramName = param[0].Trim();
                        if (isStoredProcedureReport && !isCommand)
                        {
                            paramName = "@" + paramName;
                        }
                        currValue = objReport.DataDefinition.ParameterFields[paramName].CurrentValues;
                        currValue.Add(paraValue);
                        objReport.DataDefinition.ParameterFields[paramName].ApplyCurrentValues(currValue);
                    }
                }
            }

            if (printType == ReportType.ToExcel)
            {
                DataTable dt = new DataTable();
                if (isStoredProcedureReport)
                {
                    Dictionary<string, Object> parameters = new Dictionary<string, object>();
                    //if export to excel, it need to exec the sp and get the datatable data
                    if (!string.IsNullOrEmpty(paramList))
                    {
                        var strParValPair = paramList.Split(';');
                        foreach (var item in strParValPair)
                        {
                            if (item.Contains("="))
                            {
                                var param = item.Split('=');
                                if (param[1] == "null")
                                {
                                    param[1] = null;
                                }

                                var paramName = "@" + param[0].Trim();

                                parameters.Add(paramName, param[1]);
                            }
                        }
                    }
                    string sql = reportTable.Replace(";1", "");

                    //todo: get the datatable object from database, this is just for demo so I didn't implement here :)
                    //dt = DBService.DB.GetDataSet(sql, parameters, CommandType.StoredProcedure).Result.Tables[0];

                }
                else
                {
                    dt = dataSource;
                }

                
                ExportToExcelForDownload(dt, fileName);

            }
            else
            {
                switch (printType)
                {
                    case ReportType.InlineView:
                        reportViewer.ReportSource = objReport;
                        break;
                    case ReportType.ToPDFFile:
                        objReport.ExportToHttpResponse(ExportFormatType.PortableDocFormat, response, true, fileName);
                        break;
                    case ReportType.ToWord:
                        objReport.ExportToHttpResponse(ExportFormatType.WordForWindows, response, true, fileName);
                        break;
                }
            }
        }

        /// <summary>
        /// Print report with stored procedure
        /// </summary>
        /// <param name="reportViewer">report viewer</param>
        /// <param name="reportFile">.rpt report file name and full path</param>
        /// <param name="paramList">report's parameters, string format like @name = value</param>
        /// <param name="printType">print report type</param>
        /// <param name="fileName">save as report file name</param>
        /// <param name="response">response object for save report to file</param>
        public static void PrintSPReport(CrystalReportViewer reportViewer, string reportName, string reportFile, string paramList, ReportType printType, string fileName, HttpResponse response)
        {
            PrintReport(reportViewer, reportName, reportFile, paramList, null, printType, fileName, response);
        }


        /// <summary>
        /// Print report without stored procedure
        /// </summary>
        /// <param name="reportViewer">report viewer</param>
        /// <param name="reportFile">.rpt report file name and full path</param>
        /// <param name="dataSource">dataSource for report</param>
        /// <param name="printType">print report type</param>
        /// <param name="fileName">save as report file name</param>
        /// <param name="response">response object for save report to file</param>
        public static void PrintDataSourceReport(CrystalReportViewer reportViewer, string reportName, string reportFile, string paramList, DataTable dataSource, ReportType printType, string fileName, HttpResponse response)
        {
            PrintReport(reportViewer, reportName, reportFile, paramList, dataSource, printType, fileName, response, false);
        }

        /// <summary>
        /// Create and download the excel directly
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="fileName"></param>
        private static void ExportToExcelForDownload(DataTable dt, string fileName)
        {
            using (var exportData = new MemoryStream())
            {
                //todo: export the data to excel, this is just for demo so I didn't implement here :)
                //Utility.Utility.WriteDataTableToExcel(dt, ".xls", exportData);

                fileName += ".xls";
                HttpContext.Current.Response.ClearContent();
                HttpContext.Current.Response.ContentType = "application/vnd.ms-excel";
                HttpContext.Current.Response.AddHeader("Content-Disposition", string.Format("attachment;filename={0}", fileName));
                HttpContext.Current.Response.BinaryWrite(exportData.GetBuffer());
                HttpContext.Current.Response.End();
            }
        }
    }
}