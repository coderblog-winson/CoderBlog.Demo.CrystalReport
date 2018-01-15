using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebApplication1.App_Start;

namespace WebApplication1.Reports
{
    public partial class ReportViewer : System.Web.UI.Page
    {

        protected void Page_Init(object sender, EventArgs e)
        {
            //if the source not null then just use it
            if (ReportSource.Instance.ReportDocumentObj != null)
            {
                if (IsPostBack)
                {
                    CRViewer.ReportSource = ReportSource.Instance.ReportDocumentObj;
                }
                else
                {
                    ReportSource.Instance.ReportDocumentObj.Close();
                    ReportSource.Instance.ReportDocumentObj.Dispose();
                }
            }
        }



        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                //report parameters
                var paramList = Request["Params"].Trim();
                //report file(.rpt) name
                var reportName = Request["ReportName"].Trim();
                //save as to excel,pdf or word's file name
                var saveName = Request["SaveName"].Trim();
                //report type for pdf, excel, word or inline preview
                var reportType = Request["ReportType"].Trim();
                //report file path, base on ~/Reports folder
                var reportPath = Request["ReportPath"].Trim().Replace('-', '/');

                //set the report file path
                var reportFile = Server.MapPath(string.Format("~/Reports/{0}/{1}.rpt", reportPath, reportName));
                //for save report file name
                var saveFileName = saveName + DateTime.Now.ToString("_yyyy-MM-dd");

                ReportType reType = ReportType.InlineView;
                switch (reportType.ToLower())
                {
                    case "pdf":
                        reType = ReportType.ToPDFFile;
                        break;
                    case "excel":
                        reType = ReportType.ToExcel;
                        break;
                    case "word":
                        reType = ReportType.ToWord;
                        break;
                }


                if (!IsPostBack)
                {
                    if (ReportSource.Instance.ReportData != null)
                    {
                        //if ReportData is not null then it will use datatable source to fill the report
                        ReportHandler.PrintDataSourceReport(CRViewer, reportName, reportFile, paramList, ReportSource.Instance.ReportData, reType, saveFileName, Page.Response);
                    }
                    else
                    {
                        //otherwise just call the stored procedure to fill the report
                        ReportHandler.PrintSPReport(CRViewer, reportName, reportFile, paramList, reType, saveFileName, Page.Response);
                    }
                }
            }
            catch (Exception ex)
            {
                //you can write error log here
                throw new Exception(ex.Message);
            }
        }
    }
}