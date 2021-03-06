<%@ Control Language="C#" AutoEventWireup="false" Inherits="Satrabel.OpenContent.EditGlobalSettings" CodeBehind="EditGlobalSettings.ascx.cs" %>
<%@ Register TagPrefix="dnn" TagName="Label" Src="~/controls/LabelControl.ascx" %>

<asp:Panel ID="ScopeWrapper" runat="server" CssClass="dnnForm">
    <div class="dnnFormItem">
        <dnn:Label ID="lRoles" ControlName="ddlRoles" runat="server" />
        <asp:DropDownList ID="ddlRoles" runat="server"></asp:DropDownList>
    </div>
    <div class="dnnFormItem">
        <dnn:Label ID="lMLContent" ControlName="cbMLContent" runat="server" />
        <asp:CheckBox ID="cbMLContent" runat="server" />
    </div>
    <div class="dnnFormItem">
        <dnn:Label ID="lLogging" ControlName="ddlLogging" runat="server" />
        <asp:DropDownList ID="ddlLogging" runat="server">
            <asp:ListItem Value="none" Text="None"></asp:ListItem>
            <asp:ListItem Value="host" Text="Host super"></asp:ListItem>
            <asp:ListItem Value="allways" Text="Always"></asp:ListItem>
        </asp:DropDownList>
    </div>
    <ul class="dnnActions dnnClear" style="display: block; padding-left: 35%">
        <li>
            <asp:LinkButton ID="cmdSave" runat="server" class="dnnPrimaryAction" resourcekey="cmdSave" />
        </li>
        <li>
            <asp:HyperLink ID="hlCancel" runat="server" class="dnnSecondaryAction" resourcekey="cmdCancel" />
        </li>
    </ul>
</asp:Panel>
