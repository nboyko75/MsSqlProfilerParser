using System;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MsSqlLogParse
{
    public class Parser
    {
        #region Consts
        const string AllPattern = @"(?<inlist>.*)exec sp_executesql N'(?<sql>.*?(?='))',N'(?<paramdef>.*?(?='))',(?<paramval>.*)";
        const string InlistPattern = @"declare (?<param>@p\d+) dbo.(?<list>\w+)\s+(?<inserts>insert into.*?(?=(exec\s+|declare\s+)))";
        const string InlistInsPattern = @"insert into {0} values\((?<value>.*?(?=\)))\)";
        const string ParamPrefix = "@P";
        const string ParamDelim = ",";
        const string ParamStringDelim = "'";
        #endregion

        #region Attributes
        #endregion

        #region Constructor
        public Parser() 
        {
            
        }
        #endregion

        #region Public methods
        public string ParseClipboard()
        {
            string inputStr = Clipboard.GetText();
            if (inputStr.Length == 0)
                return null;

            Regex reMain = new Regex(AllPattern, RegexOptions.Singleline);
            MatchCollection mcMain = reMain.Matches(inputStr);

            if (mcMain.Count == 0 || mcMain[0].Groups.Count < 4)
                return null;

            string inListSql = mcMain[0].Groups["inlist"].Value;
            string sSql = mcMain[0].Groups["sql"].Value;
            string sParamValStr = mcMain[0].Groups["paramval"].Value;
            List<string> ParamVals = new List<string>();
            /* put parameter values to ParamVals */
            int posDelim = sParamValStr.IndexOf(ParamDelim);
            if (posDelim > 0)
            {
                int prevPosDelim = 0;
                while (posDelim > 0)
                {
                    int nextPosDelim = prevPosDelim == 0 ? posDelim : sParamValStr.IndexOf(ParamDelim, prevPosDelim);
                    int posAps = sParamValStr.IndexOf(ParamStringDelim, prevPosDelim);
                    if (posAps > -1 && posAps < nextPosDelim)
                    {
                        posAps = sParamValStr.IndexOf(ParamStringDelim, posAps + 1);
                        while (posAps > 0 && posAps + 1 < sParamValStr.Length && sParamValStr[posAps + 1] == ParamStringDelim[0])
                        {
                            posAps = sParamValStr.IndexOf(ParamStringDelim, posAps + 2);
                        }
                        if (posAps > 0)
                        {
                            nextPosDelim = sParamValStr.IndexOf(ParamDelim, posAps + 1);
                        }
                    }
                    posDelim = nextPosDelim;
                    if (nextPosDelim == -1)
                    {
                        nextPosDelim = sParamValStr.Length - 1;
                    }
                    else
                    {
                        ParamVals.Add(sParamValStr.Substring(prevPosDelim, nextPosDelim - prevPosDelim));
                        prevPosDelim = nextPosDelim + ParamDelim.Length;
                    }
                }
                if (prevPosDelim > 0) 
                {
                    posDelim = sParamValStr.Length;
                    ParamVals.Add(sParamValStr.Substring(prevPosDelim, posDelim - prevPosDelim));
                }
            }
            else
            {
                ParamVals.Add(sParamValStr);
            }

            /* Put list parameters to ParamVals */
            if (inListSql.Length > 0)
            {
                Regex reInlist = new Regex(InlistPattern, RegexOptions.Singleline);
                MatchCollection mcInlist = reInlist.Matches(inputStr);
                if (mcInlist.Count > 0)
                {
                    foreach (Match mcInListItem in mcInlist)
                    {
                        string inListParam = mcInListItem.Groups["param"].Value;
                        string inListInserts = mcInListItem.Groups["inserts"].Value;
                        if (inListParam.Length > 0 && inListInserts.Length > 0)
                        {
                            string ptrn = String.Format(InlistInsPattern, inListParam);
                            Regex reInlistValues = new Regex(ptrn, RegexOptions.Singleline);
                            MatchCollection mcInlistValues = reInlistValues.Matches(inListInserts);
                            List<string> vals = new List<string>();
                            foreach (Match match in mcInlistValues)
                            {
                                string pVal = match.Groups["value"].Value;
                                if (pVal[0] == 'N' && pVal != "NULL")
                                {
                                    pVal = pVal.Substring(1);
                                }
                                vals.Add(pVal);
                            }
                            string paramval = string.Join(",", vals.ToArray());
                            for (int i = 0; i < ParamVals.Count; i++)
                            {
                                string val = ParamVals[i];
                                if (val == inListParam)
                                {
                                    ParamVals[i] = paramval;
                                    string inExpr = String.Format("(SELECT * FROM @P{0})", i + 1);
                                    sSql = sSql.Replace(inExpr, String.Format("({0})", paramval));
                                    break;
                                }
                            }
                        }
                    }

                }
            }

            /* Parameter values replacement */
            for (int i = ParamVals.Count; i > 0; i--)
            {
                string paramName = ParamPrefix + i.ToString();
                string paramValue = getValueStr(ParamVals[i - 1]);
                sSql = sSql.Replace(paramName, paramValue);
            }

            /* Formatting */
            SqlBlock sqlObj = new SqlBlock(sSql);

            /* Set result back to clipboard */
            Clipboard.SetText(sqlObj.GetString());
            return null;
        }
        #endregion

        #region Private methods
        private string getValueStr(string val)
        {
            string ResStr = val.Trim();
            ResStr = ResStr.Replace(" 00:00:00", "");
            if (ResStr[0] == 'N' && ResStr != "NULL") 
            {
                ResStr = ResStr.Substring(1);
            }
            return ResStr;
        }
        #endregion
    }
}
