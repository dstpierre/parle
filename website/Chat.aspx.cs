



using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.Collections.Generic;
using System.Net.Mail;
using System.Web.Services;
using System.Xml;
using System.Web.Services.Protocols;
using System.Web.Script.Services;

public partial class Chat : System.Web.UI.Page
{

    public string VisitorName
    {
        get
        {
            if (ViewState["__visitorName"] != null)
                return ViewState["__visitorName"].ToString();
            else
                return string.Empty;
        }
        set { ViewState["__visitorName"] = value; }
    }

    static public string VName
    {

        get
        {
            return HttpContext.Current.Session["VisitorName"].ToString();
        }
        set
        {
            HttpContext.Current.Session["VisitorName"] = value;
        }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!Page.IsPostBack)
        {
            pnlNoOperator.Visible = pnlChat.Visible = pnlRequest.Visible = false;

            if (OperatorService.GetOperatorStatus())
            {
                pnlRequest.Visible = true;
            }
            else
            {
                pnlNoOperator.Visible = true;
            }
        }

    }
    protected void lnkStartChat_Click(object sender, EventArgs e)
    {
        // Initiate a chat request
        string chatId = Guid.NewGuid().ToString();
        if (Request.Cookies["chatId"] != null)
        {
            Response.Cookies["chatId"].Value = chatId;
        }
        else
        {
            HttpCookie cookie = new HttpCookie("chatId", chatId);
            Response.Cookies.Add(cookie);
        }

        if (Request.Cookies[chatId + "_lastCheck"] != null)
        {
            Response.Cookies[chatId + "_lastCheck"].Value = DateTime.Now.AddMinutes(-2).Ticks.ToString();
        }
        else
        {
            HttpCookie cookie = new HttpCookie(chatId + "_lastCheck", DateTime.Now.AddMinutes(-2).Ticks.ToString());
            Response.Cookies.Add(cookie);
        }

        ChatRequestInfo request = new ChatRequestInfo();
        request.AcceptByOpereratorId = -1;
        request.ChatId = chatId;
        request.RequestDate = DateTime.Now;
        request.VisitorEmail = txtEmail.Text;
        if (Request.UserHostAddress != null)
            request.VisitorIP = Request.UserHostAddress.ToString();
        request.VisitorName = txtName.Text;
        if (Request.ServerVariables["HTTP_USER_AGENT"] != null)
            request.VisitorUserAgent = Request.ServerVariables["HTTP_USER_AGENT"].ToString();
        request.WasAccept = false;

        ChatService.RequestChat(request);

        ChatMessageInfo msg = new ChatMessageInfo(request.ChatId, string.Empty, "Waiting for an operator to accept your request.");
        ChatService.AddMessage(msg);

        // we set the visitor name in the ViewState
        VisitorName = request.VisitorName;
        VName = request.VisitorName;

        pnlChat.Visible = true;
        pnlRequest.Visible = false;
    }

    protected void timerRefresh_Tick(object sender, EventArgs e)
    {
        if (Request.Cookies["chatId"] != null)
        {
            string chatId = Request.Cookies["chatId"].Value.ToString();
            if (Request.Cookies[chatId + "_lastCheck"] != null)
            {
                long lastCheck = long.Parse(Request.Cookies[chatId + "_lastCheck"].Value.ToString());
                List<ChatMessageInfo> messages = ChatService.GetMessages(chatId, lastCheck);
                if (messages.Count > 0)
                {
                    for (int i = messages.Count - 1; i >= 0; i--)
                    {
                        litChat.Text += string.Format("<span class=\"chatName\">{0} :</span>{1}<br />", messages[i].Name, messages[i].Message);
                    }

                    lastCheck = DateTime.Now.Ticks;

                    // set the lastId
                    Response.Cookies[chatId + "_lastCheck"].Value = lastCheck.ToString();

                }
            }
        }
    }
    [System.Web.Services.WebMethod]
    [ScriptMethod(UseHttpGet = true)]
    public static string CheckTypingNotification(string chatId)
    {
        if (!string.IsNullOrEmpty(chatId))
        {
            Operator ws = new Operator();
            if (ws.IsTyping(chatId, true))
                return "The operator is typing a message... ";
            else
                return "no one typing...";
        }
        else return string.Empty;
    }

    [System.Web.Services.WebMethod]
    [ScriptMethod(UseHttpGet = true)]
    public static string SetTypingNotification(string chatId, string msg)
    {
        Operator ws = new Operator();
        ws.SetTyping(chatId, false, msg.Length > 0);
        return string.Empty;
    }

    [System.Web.Services.WebMethod]
    [ScriptMethod(UseHttpGet = true)]
    public static string SendMsg(string msg, string chtID)
    {
        if (chtID != null && msg.Length > 0)
        {
            // Add a new message to the discussion
            string chatId = chtID;

            ChatMessageInfo mesg = new ChatMessageInfo(chatId, VName, msg);
            ChatService.AddMessage(mesg);

            Operator ws = new Operator();
            ws.SetTyping(chatId, false, false);
        }
        return "";
    }



    protected void lnkSendEmail_Click(object sender, EventArgs e)
    {
        // we send an email to the configured email on the web.config
        MailMessage mail = new MailMessage();
        mail.From = new MailAddress(txtSendBy.Text);
        mail.To.Add(new MailAddress(ConfigurationManager.AppSettings["Email"].ToString()));
        mail.Subject = "Message from your LiveChat application";
        mail.Body = txtComment.Text;

        SmtpClient mailer = new SmtpClient(ConfigurationManager.AppSettings["SMTPServer"].ToString());
        mailer.Send(mail);

        lblConfirmation.Visible = true;
        lblConfirmation.Text = "Thank you, we will answer your email as soon as possible.";
    }

}