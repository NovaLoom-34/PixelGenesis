using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace PG.Governor
{
    // Parses the brain's JSON answer. Forgiving on shape, strict on types; anything unreadable is dropped.
    public static class DecisionJson
    {
        public static bool TryParse(string json, int kingdom, out GovernorDecision decision)
        {
            decision = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            string body = ExtractJson(json);
            if (body == null) return false;
            try
            {
                var obj = JObject.Parse(body);
                var d = new GovernorDecision { Kingdom = kingdom, Thought = (string)obj["thought"] ?? "" };
                if (obj["advisors"] is JArray advisors)
                    foreach (var a in advisors)
                    {
                        string s = (string)a;
                        if (!string.IsNullOrEmpty(s)) d.Advisors.Add(s.ToLowerInvariant());
                    }
                if (obj["actions"] is JArray actions)
                    foreach (var a in actions)
                    {
                        if (a is not JObject action) continue;
                        string kind = ((string)action["kind"] ?? "").Trim();
                        if (kind.Length == 0) continue;
                        // Accept both exact enum names and common synonyms.
                        switch (kind.ToLowerInvariant())
                        {
                            case "wait": case "noop": case "none":
                                d.Actions.Add(new GovernorAction(GovernorActionKind.Wait)); break;
                            case "declarewar":
                                if (TryInt(action["other"], out int other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.DeclareWar, other));
                                break;
                            case "proposepeace": case "makepeace":
                                if (TryInt(action["other"], out other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.ProposePeace, other));
                                break;
                            case "proposealliance": case "ally":
                                if (TryInt(action["other"], out other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.ProposeAlliance, other));
                                break;
                            case "proposetrade": case "proposetradeagreement":
                                if (TryInt(action["other"], out other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.ProposeTrade, other));
                                break;
                            case "proposenonaggression": case "proposenonaggressionpact":
                                if (TryInt(action["other"], out other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.ProposeNonAggression, other));
                                break;
                            case "proposevassalage": case "proposetribute":
                                if (TryInt(action["other"], out other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.ProposeVassalage, other));
                                break;
                            case "proposetradeembargo": case "proposeembargo":
                                if (TryInt(action["other"], out other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.ProposeTradeEmbargo, other));
                                break;
                            case "acceptproposal":
                                if (TryInt(action["id"], out other) || TryInt(action["other"], out other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.AcceptProposal, other));
                                break;
                            case "rejectproposal":
                                if (TryInt(action["id"], out other) || TryInt(action["other"], out other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.RejectProposal, other));
                                break;
                            case "setcapital":
                                if (TryInt(action["city"], out other) || TryInt(action["other"], out other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.SetCapital, other));
                                break;
                            case "setheir":
                                if (TryInt(action["unit"], out other) || TryInt(action["other"], out other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.SetHeir, other));
                                break;
                            case "armytarget":
                                if (TryInt(action["city"], out other) || TryInt(action["other"], out other))
                                    d.Actions.Add(new GovernorAction(GovernorActionKind.ArmyTarget, other));
                                break;
                            case "setpolicy":
                                if (action["policy"] is JObject p)
                                    d.Actions.Add(new GovernorAction(
                                        TryFloat(p["food"]), TryFloat(p["housing"]), TryFloat(p["production"]),
                                        TryFloat(p["defense"]), TryFloat(p["culture"]), TryFloat(p["tax"], 50f)));
                                break;
                            case "leavealliance":
                                d.Actions.Add(new GovernorAction(GovernorActionKind.LeaveAlliance));
                                break;
                        }
                    }
                decision = d;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Accepts a raw JSON object or one wrapped in markdown fences / prose.
        static string ExtractJson(string text)
        {
            int start = text.IndexOf('{');
            if (start < 0) return null;
            int depth = 0;
            bool inString = false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0) return text.Substring(start, i - start + 1);
            }
            return null;
        }

        static bool TryInt(JToken t, out int v)
        {
            v = -1;
            if (t == null) return false;
            if (t.Type == JTokenType.Integer) { v = t.Value<int>(); return true; }
            return int.TryParse((string)t, out v);
        }

        static float TryFloat(JToken t)
        {
            if (t == null) return 0f;
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return t.Value<float>();
            return float.TryParse((string)t, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }

        static float TryFloat(JToken t, float fallback) => t == null ? fallback : TryFloat(t);
    }
}
