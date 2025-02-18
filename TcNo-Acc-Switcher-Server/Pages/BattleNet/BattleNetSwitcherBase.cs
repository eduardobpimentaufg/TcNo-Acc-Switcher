﻿// TcNo Account Switcher - A Super fast account switcher
// Copyright (C) 2019-2022 TechNobo (Wesley Pyburn)
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

// Special thanks to iR3turnZ for contributing to this platform's account switcher
// iR3turnZ: https://github.com/HoeblingerDaniel

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using TcNo_Acc_Switcher_Globals;
using TcNo_Acc_Switcher_Server.Data;
using TcNo_Acc_Switcher_Server.Pages.General;

namespace TcNo_Acc_Switcher_Server.Pages.BattleNet
{
    public class BattleNetSwitcherBase
    {
        private static readonly Lang Lang = Lang.Instance;

        /// <summary>
        /// JS function handler for swapping to another Battle.net account.
        /// </summary>
        /// <param name="accName">Requested account's Login Username</param>
        [JSInvokable]
        public static async Task SwapToBattleNet(string accName = "")
        {
            Globals.DebugWriteLine(@"[JSInvoke:BattleNet\BattleNetSwitcherBase.SwapToBattleNet] accName:hidden");
            await BattleNetSwitcherFuncs.SwapBattleNetAccounts(accName);
        }

        /// <summary>
        /// JS function handler for swapping to a new BattleNet account (No inputs)
        /// </summary>
        [JSInvokable]
        public static async Task NewLogin_BattleNet()
        {
            Globals.DebugWriteLine(@"[JSInvoke:BattleNet\BattleNetSwitcherBase.NewLogin_BattleNet]");
            await BattleNetSwitcherFuncs.SwapBattleNetAccounts("");
        }


        public class BattleNetUser
        {
            [JsonProperty("Email", Order = 0)] public string Email { get; set; }
            [JsonProperty("BattleTag", Order = 1)] public string BTag { get; set; }
            [JsonProperty("OwTankSr", Order = 2)] public int OwTankSr { get; set; }
            [JsonProperty("OwDpsSr", Order = 3)] public int OwDpsSr { get; set; }
            [JsonProperty("OwSupportSr", Order = 4)] public int OwSupportSr { get; set; }
            [JsonProperty("OwPlayerLevel", Order = 5)] public int OwPlayerLevel { get; set; }

            [JsonProperty("LastTimeChecked", Order = 6)]
            public DateTime LastTimeChecked { get; set; }

            [JsonIgnore] public string ImgUrl { get; set; }


            public bool FetchRank()
            {
                if (!BattleNetSwitcherFuncs.ValidateBTag(BTag)) return false;
                var split = BTag.Split("#");
                var url = $"https://playoverwatch.com/en-us/career/pc/{split[0]}-{split[1]}/";

                HtmlDocument doc = new();
                if (!Globals.GetWebHtmlDocument(ref doc, url, out var responseText))
                {
                    _ = GeneralInvocableFuncs.ShowToast("error", Lang["Toast_BNet_StatsFail", new { BTag }],
                        renderTo: "toastarea");
                    return false;
                }

                //// If the PlayOverwatch site is overloaded
                //if (doc.DocumentNode.SelectSingleNode("/html/body/section[1]/section/div/h1") != null)
                //{
                //    _ = GeneralInvocableFuncs.ShowToast("error", Lang["Toast_BNet_StatsFail", new {BTag}],
                //        renderTo: "toastarea");
                //    return false;
                //}

                // Get image
                var image = doc.DocumentNode.SelectSingleNode("//img[@class='player-portrait']")?.Attributes["src"]?.Value;
                if (!string.IsNullOrEmpty(image))
                {
                    ImgUrl = image;
                    _ = BattleNetSwitcherFuncs.DownloadImage(Email, ImgUrl);
                }

                // If the Profile is private
                if (responseText.Contains("Private Profile"))
                {
                    _ = GeneralInvocableFuncs.ShowToast("warning", Lang["Toast_BNet_Private", new {BTag}], renderTo: "toastarea");
                    LastTimeChecked = DateTime.Now - TimeSpan.FromMinutes(1435); // 23 Hours 55 Minutes
                    return false;
                }

                // If the Profile is not found
                if (responseText.Contains("Profile Not Found"))
                {
                    _ = GeneralInvocableFuncs.ShowToast("error", Lang["Toast_BNet_NotFound", new {BTag}], renderTo: "toastarea");
                    return false;
                }

                // If BattleTag is invalid
                if (doc.DocumentNode.SelectSingleNode("//h1[@class='u-align-center']")?.InnerHtml == "PROFILE NOT FOUND")
                {
                    _ = GeneralInvocableFuncs.ShowToast("error", Lang["Toast_ItemNotFound", new {item = BTag}], renderTo: "toastarea");
                    return false;
                }

                LastTimeChecked = DateTime.Now;

                // Get player level
                var playerLevelStr = doc.DocumentNode.SelectNodes("//div[@class='player-level']/div[@class='u-vertical-center']")?.FirstOrDefault()?.InnerHtml;
                if (int.TryParse(playerLevelStr, out var playerLevel))
                    OwPlayerLevel = playerLevel;

                // Get Overwatch ranks
                var ranks = doc.DocumentNode.SelectNodes("//div[@class='competitive-rank-role']");
                if (ranks is null) return true; // No ranks found
                foreach (var node in ranks.Elements())
                {
                    //if (!int.TryParse(node.LastChild.LastChild.InnerHtml, out var sr))
                    if (!int.TryParse(node.SelectNodes("div[@class='competitive-rank-level']")?[0]?.InnerHtml, out var sr))
                    {
                        continue;
                    }

                    var innerHtml = node.InnerHtml;
                    if (innerHtml.Contains("Tank"))
                        OwTankSr = sr;
                    else if (innerHtml.Contains("Damage"))
                        OwDpsSr = sr;
                    else if (innerHtml.Contains("Support"))
                        OwSupportSr = sr;
                }

                return true;
            }
        }
    }
}