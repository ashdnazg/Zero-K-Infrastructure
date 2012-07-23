using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc.Html;
using ZeroKWeb;
using ZkData;

namespace System.Web.Mvc
{
    public enum StarType
    {
        RedStarSmall,
        GreenStarSmall,
        WhiteStarSmall,
        RedSkull,
        WhiteSkull
    }

    public class SelectOption
    {
        public string Name;
        public string Value;
    }

    public static class HtmlHelperExtensions
    {

        public static IEnumerable<SelectListItem> GetFactionItems(this HtmlHelper html, int factionID, Expression<Func<Faction, bool>> filter = null)
        {
            var ret = new ZkDataContext().Factions.AsQueryable().Where(x => !x.IsDeleted);
            if (filter != null) ret = ret.Where(filter);
            return ret.Select(x => new SelectListItem { Text = x.Name, Value = x.FactionID.ToString(), Selected = x.FactionID == factionID });
        }


        public static MvcHtmlString AccountAvatar(this HtmlHelper helper, Account account)
        {
            return
                new MvcHtmlString(string.Format("<img src='/img/avatars/{0}.png' class='avatar'>", account.Avatar));
        }

        // todo all calls must provide helper!
        public static MvcHtmlString BBCode(this HtmlHelper helper, string str)
        {
            if (str == null) return null;


            var db = new ZkDataContext();
            str = Regex.Replace(str, @"@(\w+)", m =>
            {
                var val = m.Groups[1].Value;
                var acc = Account.AccountByName(db, val);
                if (acc != null) return PrintAccount(null, acc).ToString();
                var clan = db.Clans.FirstOrDefault(x => x.Shortcut == val);
                if (clan != null) return PrintClan(null, clan).ToString();
                var fac = db.Factions.FirstOrDefault(x => x.Shortcut == val);
                if (fac != null) return PrintFaction(null, fac, false).ToString();

                if (val.StartsWith("b", StringComparison.InvariantCultureIgnoreCase))
                {
                    var bid = 0;
                    if (int.TryParse(val.Substring(1), out bid))
                    {
                        var bat = db.SpringBattles.FirstOrDefault(x => x.SpringBattleID == bid);
                        if (bat != null) return PrintBattle(null, bat).ToString();
                    }
                }
                return "@" + val;
            });

            Regex exp;
            // format the bold tags: [b][/b]
            // becomes: <strong></strong>
            exp = new Regex(@"\[b\](.+?)\[/b\]");
            str = exp.Replace(str, "<strong>$1</strong>");

            // format the quote tags: [quote][/quote]
            // becomes: <em></em>
            exp = new Regex(@"\[quote\](.+?)\[/quote\]");
            str = exp.Replace(str, "<table border=\"0\" cellpadding=\"6\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td style=\"border: 1px inset;\"><em>quote:<br>$1</em></td></tr></tbody></table>");
 			
			
            // format the italic tags: [i][/i]
            // becomes: <em></em>
            exp = new Regex(@"\[i\](.+?)\[/i\]");
            str = exp.Replace(str, "<em>$1</em>");

            // format the underline tags: [u][/u]
            // becomes: <u></u>
            exp = new Regex(@"\[u\](.+?)\[/u\]");
            str = exp.Replace(str, "<u>$1</u>");

            // format the strike tags: [s][/s]
            // becomes: <strike></strike>
            exp = new Regex(@"\[s\](.+?)\[/s\]");
            str = exp.Replace(str, "<strike>$1</strike>");

            // format the url tags: [url=www.website.com]my site[/url]
            // becomes: <a href="www.website.com">my site</a>
            exp = new Regex(@"\[url\=([^\]]+)\]([^\]]+)\[/url\]");
            str = exp.Replace(str, "<a href=\"$1\">$2</a>");

            // format the img tags: [img]www.website.com/img/image.jpeg[/img]
            // becomes: <img src="www.website.com/img/image.jpeg" />
            exp = new Regex(@"\[img\]([^\[]+)\[/img\]");
            str = exp.Replace(str, "<img src=\"$1\" />");

            // format img tags with alt: [img=www.website.com/img/image.jpeg]this is the alt text[/img]
            // becomes: <img src="www.website.com/img/image.jpeg" alt="this is the alt text" />
            exp = new Regex(@"\[img\=([^\]]+)\]([^\]]+)\[/img\]");
            str = exp.Replace(str, "<img src=\"$1\" alt=\"$2\" />");

            //format the colour tags: [color=red][/color]
            // becomes: <font color="red"></font>
            // supports UK English and US English spelling of colour/color
            exp = new Regex(@"\[color\=([^\]]+)\]([^\]]+)\[/color\]");
            str = exp.Replace(str, "<font color=\"$1\">$2</font>");
            exp = new Regex(@"\[colour\=([^\]]+)\]([^\]]+)\[/colour\]");
            str = exp.Replace(str, "<font color=\"$1\">$2</font>");

            // format the size tags: [size=3][/size]
            // becomes: <font size="+3"></font>
            exp = new Regex(@"\[size\=([^\]]+)\]([^\]]+)\[/size\]");
            str = exp.Replace(str, "<font size=\"+$1\">$2</font>");


           

            str = Regex.Replace(str, @"(^|[\s])((mailto|spring|http|https|ftp|ftps)\://\S+)", @"$1<a href='$2'>$2</a>");

            // lastly, replace any new line characters with <br />
            str = str.Replace("\r\n", "<br />\r\n");

            if (helper != null)
            { // todo remove condition in the future
                exp = new Regex(@"\[poll\]([0-9]+)\[/poll\]");
                str = exp.Replace(str, m => helper.Action("Index", "Poll", new { pollID = m.Groups[1].Value }).ToHtmlString());
            }

            
            return new MvcHtmlString(str);
        }

        public static MvcHtmlString BoolSelect(this HtmlHelper helper, string name, bool? selected, string anyItem)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("<select name='{0}'>", helper.Encode(name));
            if (anyItem != null) sb.AppendFormat("<option {1}>{0}</option>", helper.Encode(anyItem), selected == null ? "selected" : "");
            sb.AppendFormat("<option value='True' {0}>Yes</option>", selected == true ? "selected" : "");
            sb.AppendFormat("<option value='False' {0}>No</option>", selected == false ? "selected" : "");

            sb.Append("</select>");
            return new MvcHtmlString(sb.ToString());
        }

        public static MvcHtmlString IncludeFile(this HtmlHelper helper, string name)
        {
            if (name.StartsWith("http://"))
            {
                var ret = new WebClient().DownloadString(name);
                return new MvcHtmlString(ret);
            }
            else
            {
                var path = HttpContext.Current.Server.MapPath(name);
                return new MvcHtmlString(File.ReadAllText(path));
            }
        }

        public static MvcHtmlString Print(this HtmlHelper helper, ForumThread thread)
        {
            var url = new UrlHelper(HttpContext.Current.Request.RequestContext);
            var link = url.Action("Thread", "Forum", new { id = thread.ForumThreadID });
            link = string.Format("<a href='{0}' title='$thread${1}'>",link, thread.ForumThreadID);
            var lastRead = thread.ForumThreadLastReads.FirstOrDefault(x=>x.AccountID == Global.AccountID);

            string format;
            
            if (lastRead == null)
            {
                format = "<span>{0}<img src='/img/mail/mail-unread.png' height='15' /><i>{1}</i></a></span>";
            }
            else {
                if (lastRead.LastRead >= thread.LastPost)
                {
                    format = "<span>{0}<img src='/img/mail/mail-read.png' height='15' />{1}</a></span>";
                }
                else {
                    if (lastRead.LastPosted != null) format = "<span>{0}<img src='/img/mail/mail-new.png' height='15' /><b>{1}</b></a></span>";
                    else format = "<span>{0}<img src='/img/mail/mail-unread.png' height='15' />{1}</a></span>";
                }
            }

            return new MvcHtmlString(string.Format(format,link, thread.Title));
        }


        public static MvcHtmlString IncludeWiki(this HtmlHelper helper, string node)
        {
            return new MvcHtmlString(WikiHandler.LoadWiki(node, "", true));
        }

        public static MvcHtmlString PrintAccount(this HtmlHelper helper, Account account, bool colorize = true)
        {
            if (account == null) return new MvcHtmlString("Nobody");
            else
            {
                var clanStr = "";
                var url = new UrlHelper(HttpContext.Current.Request.RequestContext);
                if (account.Clan != null) {
                    clanStr = string.Format("<a href='{1}' nicetitle='$clan${2}'><img src='{0}' width='16'/></a>", account.Clan.GetImageUrl(), url.Action("Detail", "Clans", new { id= account.ClanID}), account.ClanID);
                }
                else if (account.Faction != null) {
                    clanStr = string.Format("<img src='{0}' width='16'/>", account.Faction.GetImageUrl());
                }

                var clampedLevel = account.Level / 10 + 1;
                if (clampedLevel < 1) clampedLevel = 1;
                if (clampedLevel > 9) clampedLevel = 9;

                return
                    new MvcHtmlString(
                        string.Format(
                            "<img src='/img/flags/{0}.png' class='flag' height='11' width='16' alt='{0}'/><img src='/img/ranks/{1}.png'  class='icon16' alt='rank' />{5}<a href='/Users/Detail/{2}' style='color:{3}' nicetitle='$user${2}'>{4}</a>",
                            account.Country != "??" ? account.Country : "unknown",
                            clampedLevel,
                            account.AccountID,
                            colorize ? Faction.FactionColor(account.Faction, Global.FactionID) : "",
                            account.Name,clanStr));
            }
        }

       

        public static MvcHtmlString PrintBattle(this HtmlHelper helper, SpringBattlePlayer battlePlayer)
        {
            return PrintBattle(helper, battlePlayer.SpringBattle, battlePlayer.IsSpectator ? null : (bool?)battlePlayer.IsInVictoryTeam);
        }

        public static MvcHtmlString PrintBattle(this HtmlHelper helper, SpringBattle battle, bool? isVictory = null)
        {
            var url = new UrlHelper(HttpContext.Current.Request.RequestContext);
            var icon = "";
            if (isVictory == true) icon = "battlewon.png";
            else if (isVictory == null) icon = "spec.png";
            else icon = "battlelost.png";

            icon = string.Format("<img src='/img/battles/{0}' class='vcenter' />", icon);


            if (battle.IsMission) icon += " <img src='/img/battles/mission.png' alt='Mission' class='vcenter' />";
            if (battle.HasBots) icon += " <img src='/img/battles/robot.png' alt='Bots' class='vcenter' />";

            if (battle.BattleType == "Multiplayer") icon += " <img src='/img/battles/multiplayer.png' alt='Multiplayer' class='vcenter' />";
            else if (battle.BattleType == "Singleplayer") icon += " <img src='/img/battles/singleplayer.png' alt='Singleplayer' class='vcenter' />";

            return
                new MvcHtmlString(string.Format("<span><a href='{0}'>{4} B{1}</a> {2} on {3}</span>",
                                                url.Action("Detail", "Battles", new { id = battle.SpringBattleID }),
                                                battle.SpringBattleID,
                                                battle.PlayerCount,
                                                PrintMap(helper, battle.ResourceByMapResourceID.InternalName),
                                                icon));

        }


        public static MvcHtmlString PrintClan(this HtmlHelper helper, Clan clan, bool colorize = true)
        {
            var url = new UrlHelper(HttpContext.Current.Request.RequestContext);
            if (clan == null) return new MvcHtmlString(string.Format("<a href='{0}'>No Clan</a>", url.Action("Index", "Clans")));
            {
                return
                    new MvcHtmlString(string.Format("<a href='{0}' nicetitle='$clan${4}'><img src='{1}' width='16'><span style='color:{2}'>{3}</span></a>",
                                                    url.Action("Detail", "Clans", new { id = clan.ClanID }),
                                                    clan.GetImageUrl(),
                                                    colorize ? Clan.ClanColor(clan, Global.ClanID) : "",
                                                    clan.Shortcut, clan.ClanID));
            }
        }

        public static MvcHtmlString PrintRoleType(this HtmlHelper helper, RoleType rt) {

            List<string> factoids = new List<string>();
            if (rt.IsClanOnly)
            {
                factoids.Add("clan based");
            }
            if (rt.IsOnePersonOnly)
            {
                factoids.Add("only one person can hold this");
            }

            if (rt.IsVoteable) {
                factoids.Add("is voteable");
            }
            if (rt.RoleTypeHierarchiesByMasterRoleTypeID.Any(x=>x.CanAppoint))
            {
                factoids.Add("appoints: " + string.Join(",", rt.RoleTypeHierarchiesByMasterRoleTypeID.Where(x=>x.CanAppoint).Select(x=>x.RoleTypeBySlaveRoleTypeID.Name)));
            }
            if (rt.RoleTypeHierarchiesByMasterRoleTypeID.Any(x=>x.CanRecall)) {
                factoids.Add("recalls: " + string.Join(",", rt.RoleTypeHierarchiesByMasterRoleTypeID.Where(x => x.CanAppoint).Select(x => x.RoleTypeBySlaveRoleTypeID.Name)));
            }
            if (rt.RightBomberQuota != 0)
            {
                factoids.Add(string.Format("bomber quota {0:F0}%", rt.RightBomberQuota * 100));
            }
            if (rt.RightDropshipQuota != 0)
            {
                factoids.Add(string.Format("dropship quota {0:F0}%", rt.RightDropshipQuota * 100));
            }
            if (rt.RightMetalQuota != 0)
            {
                factoids.Add(string.Format("metal quota {0:F0}%", rt.RightMetalQuota * 100));
            }
            if (rt.RightSetEnergyPriority)
            {
                factoids.Add("can set energy priorities");
            }
            if (rt.RightDiplomacy)
            {
                factoids.Add("can control diplomacy");
            }
            if (rt.RightEditTexts)
            {
                factoids.Add("controls texts");
            }
            return new MvcHtmlString(string.Format("<span title=\"<b>{0}</b><ul>{1}</ul>\"><b>{2}</b></span>", rt.Description, string.Join("",factoids.Select(x=>"<li>" + x + "</li>")), rt.Name));
        }

        public static MvcHtmlString PrintFaction(this HtmlHelper helper, Faction fac, bool big = true)
        {
            var url = new UrlHelper(HttpContext.Current.Request.RequestContext);
            if (fac != null)
            {
                if (big) return new MvcHtmlString(string.Format("<a href='{1}' nicetitle='$faction${2}'><img src='{0}'/></a>", fac.GetImageUrl(), url.Action("Detail", "Factions", new { id = fac.FactionID }), fac.FactionID));
                else
                {
                    return
                        new MvcHtmlString(string.Format(
                            "<a href='{3}' nicetitle='$faction${4}'><span style='color:{0}'><img src='{1}'  style='width:16px;height:16px'/>{2}</span></a>",
                            fac.Color,
                            fac.GetImageUrl(),
                            fac.Shortcut, url.Action("Detail","Factions", new{id = fac.FactionID}), fac.FactionID));
                }
            }
            else return new MvcHtmlString("");
        }

        public static MvcHtmlString PrintFactionTreaty(this HtmlHelper helper, FactionTreaty treaty)
        {
            var url = new UrlHelper(HttpContext.Current.Request.RequestContext);
            if (treaty != null)
            {
                
                    return
                        new MvcHtmlString(string.Format(
                            "<a href='{1}' nicetitle='$treaty${0}'>TR{0}</span></a>",
                            treaty.FactionTreatyID,
                             url.Action("TreatyDetail", "Factions", new { id = treaty.FactionTreatyID })));
            }
            else return new MvcHtmlString("");
        }


        public static MvcHtmlString PrintInfluence(this HtmlHelper helper, AccountPlanet accountPlanet)
        {
            return PrintInfluence(helper, accountPlanet.Account.Clan, accountPlanet.Influence, accountPlanet.ShadowInfluence);
        }

        public static MvcHtmlString PrintInfluence(this HtmlHelper helper, Clan clan, int influence, int shadowInfluence)
        {
            var formatString = "<span style='color:{0}'>{1}</span>";
            if (shadowInfluence > 0) formatString += "&nbsp({2}&nbsp+&nbsp<span style='color:gray'>{3}</span>)";
            var formattedString = string.Format(formatString,
                                                Clan.ClanColor(clan, Global.ClanID),
                                                influence + shadowInfluence,
                                                influence,
                                                shadowInfluence);
            return new MvcHtmlString(formattedString);
        }

        public static MvcHtmlString PrintInfluence(this HtmlHelper helper, Faction faction, int influence, int shadowInfluence)
        {
            var formatString = "<span style='color:{0}'>{1}</span>";
            if (shadowInfluence > 0) formatString += "&nbsp({2}&nbsp+&nbsp<span style='color:gray'>{3}</span>)";
            var formattedString = string.Format(formatString, faction.Color, influence + shadowInfluence, influence, shadowInfluence);
            return new MvcHtmlString(formattedString);
        }

        public static MvcHtmlString PrintLines(this HtmlHelper helper, string text)
        {
            return new MvcHtmlString(helper.Encode(text).Replace("\n", "<br/>"));
        }

        public static MvcHtmlString PrintLines(this HtmlHelper helper, IEnumerable<object> lines)
        {
            var sb = new StringBuilder();
            foreach (var line in lines) sb.AppendFormat("{0}<br/>", line);
            return new MvcHtmlString(sb.ToString());
        }

        public static MvcHtmlString PrintMap(this HtmlHelper helper, string name)
        {
            var url = new UrlHelper(HttpContext.Current.Request.RequestContext);
            return new MvcHtmlString(string.Format("<a href='{0}' title='$map${1}'>{1}</a>", url.Action("DetailName", "Maps", new { name }), name));
        }

        public static MvcHtmlString PrintMetalCost(this HtmlHelper helper, int? cost)
        {
            const string metalIcon = "http://zero-k.googlecode.com/svn/trunk/mods/zk/LuaUI/Images/ibeam.png";
            return new MvcHtmlString(string.Format("<span style='color:#00FFFF;'>{0}<img src='{1}' width='20' height='20'/></span>", cost, metalIcon));
        }

        public static MvcHtmlString PrintPlanet(this HtmlHelper helper, Planet planet)
        {
            if (planet == null) return new MvcHtmlString("?");
            var url = new UrlHelper(HttpContext.Current.Request.RequestContext);
            return
                new MvcHtmlString(string.Format("<a href='{0}' title='$planet${4}'><img src='/img/planets/{1}' width='{2}'>{3}</a>",
                                                url.Action("Planet", "Planetwars", new { id = planet.PlanetID }),
                                                planet.Resource.MapPlanetWarsIcon,
                                                planet.Resource.PlanetWarsIconSize/3,
                                                planet.Name,
                                                planet.PlanetID));
        }

        public static MvcHtmlString PrintTreaty(this HtmlHelper helper, AllyStatus status, bool isResearh, int givenInfluence)
        {
            return
                new MvcHtmlString(string.Format("<span style='color:{0}'>{1}</span> Influence given: {4} <span style='color:{2}'>{3}</span>",
                                                Clan.AllyStatusColor(status),
                                                status,
                                                isResearh ? "#00FF00" : "#FF0000",
                                                isResearh ? "research shared" : "", givenInfluence));
        }

        public static MvcHtmlString PrintEffectiveTreaty(this HtmlHelper helper, AllyStatus status, bool isResearh, double balance)
        {
            return
                new MvcHtmlString(string.Format("<span style='color:{0}'>{1}</span> Influence balance: {4}% <span style='color:{2}'>{3}</span>",
                                                Clan.AllyStatusColor(status),
                                                status,
                                                isResearh ? "#00FF00" : "#FF0000",
                                                isResearh ? "research shared" : "", Math.Round(balance*100)));
        }

        public static MvcHtmlString PrintTreaty(this HtmlHelper helper, EffectiveTreaty treaty)
        {
            return PrintEffectiveTreaty(helper, treaty.AllyStatus, treaty.IsResearchAgreement, treaty.InfluenceGivenToSecondClanBalance);
        }

        public static MvcHtmlString PrintTreaty(this HtmlHelper helper, TreatyOffer treaty)
        {
            return PrintTreaty(helper, treaty.AllyStatus, treaty.IsResearchAgreement, treaty.InfluenceGiven);
        }

        public static MvcHtmlString Select(this HtmlHelper helper, string name, Type etype, int? selected, string anyItem)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("<select name='{0}'>", helper.Encode(name));
            var names = Enum.GetNames(etype);
            var values = (int[])Enum.GetValues(etype);
            if (anyItem != null) sb.AppendFormat("<option {1}>{0}</option>", helper.Encode(anyItem), selected == null ? "selected" : "");
            for (var i = 0; i < names.Length; i++)
                sb.AppendFormat("<option value='{0}' {2}>{1}</option>",
                                helper.Encode(values[i]),
                                helper.Encode(names[i]),
                                selected == values[i] ? "selected" : "");
            sb.Append("</select>");
            return new MvcHtmlString(sb.ToString());
        }


        public static MvcHtmlString Select(this HtmlHelper helper, string name, IEnumerable<SelectOption> items, string selected)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("<select name='{0}'>", helper.Encode(name));
            foreach (var item in items)
                sb.AppendFormat("<option value='{0}' {2}>{1}</option>",
                                helper.Encode(item.Value),
                                helper.Encode(item.Name),
                                selected == item.Value ? "selected" : "");
            sb.Append("</select>");
            return new MvcHtmlString(sb.ToString());
        }

        public static MvcHtmlString Stars(this HtmlHelper helper, StarType type, double? rating)
        {
            if (rating.HasValue)
            {
                var totalWidth = 5*14;
                var starWidth = (int)(rating*14.0);
                return
                    new MvcHtmlString(string.Format("<span class='{0}' style='width:{1}px'></span><span style='width:{3}px'></span>",
                                                    type,
                                                    starWidth,
                                                    rating,
                                                    totalWidth - starWidth));
            }
            else
            {
                return
                    new MvcHtmlString(string.Format("<span class='{0}' style='width:70px' title='No votes'></span>",
                                                    type == StarType.RedSkull ? StarType.WhiteSkull : StarType.WhiteStarSmall));
            }
        }


        public static string ToAgoString(this DateTime? utcDate)
        {
            if (utcDate.HasValue) return ToAgoString(DateTime.UtcNow.Subtract(utcDate.Value));
            else return "";
        }

        public static string ToAgoString(this DateTime utcDate)
        {
            return ToAgoString(DateTime.UtcNow.Subtract(utcDate));
        }

        public static string ToAgoString(this TimeSpan timeSpan)
        {
            return string.Format("{0} ago", timeSpan.ToNiceString());
        }

        public static string ToNiceString(this TimeSpan timeSpan)
        {
            if (timeSpan.TotalMinutes < 2) return string.Format("{0} seconds", (int)timeSpan.TotalSeconds);
            if (timeSpan.TotalHours < 2) return string.Format("{0} minutes", (int)timeSpan.TotalMinutes);
            if (timeSpan.TotalDays < 2) return string.Format("{0} hours", (int)timeSpan.TotalHours);
            if (timeSpan.TotalDays < 60) return string.Format("{0} days", (int)timeSpan.TotalDays);
            return string.Format("{0} months", (int)(timeSpan.TotalDays/30));
        }
    }
}