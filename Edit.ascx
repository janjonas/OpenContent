<%@ Control Language="C#" AutoEventWireup="false" Inherits="Satrabel.OpenContent.Edit" CodeBehind="Edit.ascx.cs" %>
<%@ Import Namespace="Newtonsoft.Json" %>

<asp:Panel ID="ScopeWrapper" runat="server">
    <div id="field1" class="alpaca"></div>
    <ul class="dnnActions dnnClear" style="display: block; padding-left: 35%">
        <li>
            <asp:HyperLink ID="cmdSave" runat="server" class="dnnPrimaryAction" resourcekey="cmdSave" />
        </li>
        <li>
            <asp:HyperLink ID="hlCancel" runat="server" class="dnnSecondaryAction" resourcekey="cmdCancel" />
        </li>
        <li>
            <asp:HyperLink ID="hlDelete" runat="server" class="dnnSecondaryAction" resourcekey="cmdDelete" />
        </li>
        <li style="padding-left: 10px;">
            <asp:DropDownList ID="ddlVersions" runat="server" CssClass="oc-ddl-versions" />
        </li>
    </ul>
</asp:Panel>

<script type="text/javascript">
    $(document).ready(function() {
        var engine = new alpacaEngine.engine(<%=JsonConvert.SerializeObject(AlpacaContext)%>);
        engine.init();
    });
</script>
