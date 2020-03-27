using System;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MsSqlLogParse
{
    public class SqlPart 
    {
        public int id;
        public int parent_id;
        public int Begin;
        public int End;
        public int Level;
        public int BeginOffset;
        public int EndOffset;
    }

    public class SqlKey 
    {
        public string Key;
        public bool IsGroup;
        public int IdentBefore;
        public int IdentAfter;
        public bool breakLineBefore;
        public bool breakLineAfter;
        public bool endIsInPart;
        public SqlKey(string aKey, bool aIsGroup, int aIdentBefore, int aIdentAfter, bool aBreakLineBefore, bool aBreakLineAfter, bool aEndIsInPart) 
        {
            Key = aKey;
            IsGroup = aIsGroup;
            IdentBefore = aIdentBefore;
            IdentAfter = aIdentAfter;
            breakLineBefore = aBreakLineBefore;
            breakLineAfter = aBreakLineAfter;
            endIsInPart = aEndIsInPart;
        }
    }

    public class SqlBlock
    {
        #region Consts
        public const string BR = "\r\n";
        public const string NEWLINE = "\n";
        public const string TAB = "\t";
        public const char CTAB = '\t';
        public const string BRTAB = "\r\n\t";
        public List<SqlKey> EnterKeys = new List<SqlKey>() { new SqlKey("SELECT", true, 0, 0, true, false, false), new SqlKey("FROM", true, 0, 0, true, false, false),
            new SqlKey("LEFT JOIN", true, 1, 1, true, false, false), new SqlKey("INNER JOIN", true, 1, 1, true, false, false), new SqlKey("WHERE", true, 0, 0, true, false, false),
            new SqlKey("GROUP BY", true, 0, 0, true, false, false), new SqlKey("ORDER BY", true, 0, 0, true, false, false), new SqlKey("AND", false, 1, 0, true, false, false),
            new SqlKey("EXISTS", true, 0, 1, false, false, true), new SqlKey("NOT EXISTS", true, 0, 1, false, false, true), new SqlKey("VALUES", true, 0, 1, true, false, false) };
        public const string PartBegin = "(";
        public const string PartEnd = ")";
        public const string PartEdge = @"\(|\)|$";
        public const int LineWidth = 120;
        public const int LineWidthOverSize = 10;
        #endregion

        #region Attributes
        private string sql;
        private int sqlLen;
        private List<SqlPart> parts = new List<SqlPart>();
        private List<SqlPart> identBlocks = new List<SqlPart>();
        #endregion

        #region Constructor
        public SqlBlock(string inputSql)
        {
            this.sql = inputSql.Replace("''", "'").Replace(NEWLINE, NEWLINE + TAB);
            sqlLen = this.sql.Length;
            parse();
        }
        #endregion

        #region Public methods
        public string GetString()
        {
            return this.sql;
        }
        #endregion

        #region Private methods
        private void parse()
        {
            parseParts();
            parseIdentBlocks();

            /* Відступи по рівню вкладеності */
            SortedDictionary<int, string> insStr = new SortedDictionary<int, string>();
            foreach (SqlKey sqlkey in EnterKeys)
            {
                string prefix = new String(CTAB, sqlkey.IdentBefore);
                string regexPattern = String.Format(@"(^|\W)(?i){0}(?-i)\W", sqlkey.Key);
                Regex rgx = new Regex(regexPattern);
                MatchCollection matches = rgx.Matches(this.sql);
                foreach (Match match in matches)
                {
                    if (match.Index > 0)
                    {
                        int pos = match.Index == 0 ? match.Index : match.Index + 1;
                        int lvl = getLevel4Pos(pos);
                        int blockIdents = getBlockIdent4Pos(pos);
                        string insertStr = sqlkey.breakLineBefore ? BR + prefix + new String(CTAB, lvl) + new String(CTAB, blockIdents) : "";
                        if (this.sql[pos - 1].ToString() == PartBegin)
                        {
                            /* щоб переносити "(SELECT" разом зі скобкою */
                            pos--;
                        }
                        insStr.Add(pos, insertStr);
                        if (sqlkey.breakLineAfter) 
                        {
                            /* Для блоків "EXISTS" та "NOT EXISTS" шукати кінець блоку і зробити перенос після нього */
                            int blockBegPos = this.sql.IndexOf(PartBegin, pos + match.Value.Length - 1);
                            if (blockBegPos > 0) 
                            {
                                SqlPart blockPart = getSqlPartEnd4Pos(blockBegPos);
                                if (blockPart != null && blockPart.End < this.sql.Length - 1) 
                                {
                                    pos = blockPart.End + 1;
                                    int commaIdx = this.sql.IndexOf(",", pos);
                                    if (commaIdx > 0)
                                    {
                                        string blockTail = this.sql.Substring(pos, commaIdx - pos + 1).Trim();
                                        if (blockTail.Length == 0)
                                        {
                                            pos = commaIdx + 1;
                                        }
                                    }
                                    lvl = getLevel4Pos(pos);
                                    blockIdents = getBlockIdent4Pos(pos);
                                    insStr.Add(pos, BR + new String(CTAB, lvl) + new String(CTAB, blockIdents));
                                }
                            }
                        }
                    }
                }
            }
            int offset = 0;
            foreach (KeyValuePair<int, string> pair in insStr) 
            {
                this.sql = this.sql.Insert(pair.Key + offset, pair.Value);
                offset += pair.Value.Length;
                setSqlPartOffset(pair.Key, pair.Value.Length);
                setBlockIdentOffset(pair.Key, pair.Value.Length);
            }

            checkLineWidth();
        }

        /* Встановлення відступів для блоків з дужками (...) */
        private void parseParts()
        {
            parts.Clear();
            if (sqlLen <= LineWidth)
            {
                return;
            }

            int prevPos;
            int nextPos = -1;
            int id = 1;
            string prevEdge = "";
            SqlPart prevPart = new SqlPart();
            prevPart.id = id;
            prevPart.Level = -1;
            Regex rePart = new Regex(PartEdge, RegexOptions.Singleline);
            do
            {
                prevPos = nextPos + 1;
                SqlPart part = new SqlPart();

                if (prevPos >= sqlLen) 
                {
                    break;
                }
                Match mcPart = rePart.Match(this.sql, prevPos);
                if (mcPart.Success)
                {
                    nextPos = mcPart.Index;
                    string partEdge = mcPart.Groups[0].Value;
                    if (prevEdge != PartEnd)
                    {
                        part.id = id++;
                        part.parent_id = prevPart.id;
                        part.Level = prevPart.Level + 1;
                        prevPart = part;
                    }
                    else
                    {
                        prevPart = getSqlPartById(prevPart.parent_id);
                        part.id = prevPart.id;
                        part.parent_id = prevPart.parent_id;
                        part.Level = prevPart.Level;
                    }
                    prevEdge = partEdge;
                }
                else
                {
                    break;
                }
                if (nextPos > -1)
                {
                    part.Begin = prevPos;
                    part.End = nextPos;
                    parts.Add(part);
                }
            } while (nextPos > -1);
        }

        /* Встановлення відступів для блоків LEFT JOIN, INNER JOIN, EXISTS */
        private void parseIdentBlocks()
        {
            identBlocks.Clear();
            List<string> identBlocksAfter = new List<string>();
            List<string> identGroups = new List<string>();
            foreach (SqlKey sqlkey in EnterKeys) 
            {
                if (sqlkey.IdentAfter > 0) 
                {
                    identBlocksAfter.Add(sqlkey.Key);
                }
                if (sqlkey.IsGroup)
                {
                    identGroups.Add(sqlkey.Key);
                }
            }
            string patternBegin = String.Format(@"(^|\W)(?i)({0})(?-i)\W", string.Join("|", identBlocksAfter.ToArray()));
            Regex reBeg = new Regex(patternBegin, RegexOptions.Singleline);
            string patternEnd = String.Format(@"\W(?i)({0}|$)(?-i)\W", string.Join("|", identGroups.ToArray()));
            Regex reEnd = new Regex(patternEnd, RegexOptions.Singleline);
            int partBegPos = -1;
            int prevBegPos = 0;
            int lastPos = this.sql.Length - 1;
            int partEndPos = lastPos;
            int id = 1;
            int lvl;
            do
            {
                Match mcBeg = reBeg.Match(this.sql, prevBegPos);
                if (mcBeg.Success)
                {
                    int keyStartPos = mcBeg.Index == 0 ? 0 : 1;
                    int matchLen = mcBeg.Length - keyStartPos - 1;
                    partBegPos = mcBeg.Index + matchLen + 1;
                    partEndPos = lastPos;
                    string key = mcBeg.Value.Substring(keyStartPos, matchLen);
                    SqlKey sqlkey = getEnterKey4Key(key);
                    bool endIsInPart = sqlkey != null && sqlkey.endIsInPart;
                    lvl = 0;
                    SqlPart partBeg = getSqlPart4Pos(partBegPos);
                    if (partBeg != null)
                    {
                        if (endIsInPart)
                        {
                            int blockBegPos = this.sql.IndexOf(PartBegin, partBegPos);
                            if (blockBegPos > 0)
                            {
                                SqlPart blockPart = getSqlPartEnd4Pos(blockBegPos + 1);
                                if (blockPart != null && blockPart.End < this.sql.Length - 1)
                                {
                                    partEndPos = blockPart.End;
                                }
                            }
                            lvl = sqlkey != null ? sqlkey.IdentAfter : 1;
                        }
                        else
                        {
                            MatchCollection mcEnds = reEnd.Matches(this.sql, partBegPos);
                            for (int i = 0; i < mcEnds.Count; i++)
                            {
                                Match mcItem = mcEnds[i];
                                int endPos = mcItem.Index + 1;
                                SqlPart partEnd = getSqlPart4Pos(endPos);
                                if (partEnd != null && partEnd.id == partBeg.id)
                                {
                                    partEndPos = mcItem.Index;
                                    lvl = 1;
                                    break;
                                }
                            }
                        }

                        SqlPart part = new SqlPart();
                        part.id = id++;
                        part.parent_id = part.id;
                        part.Begin = partBegPos;
                        part.End = partEndPos;
                        part.Level = lvl;
                        identBlocks.Add(part);
                    }
                    else
                    {
                        partBegPos = -1;
                        break;
                    }
                    prevBegPos = partBegPos;
                }
                else 
                {
                    partBegPos = -1;
                    break;
                }
            } while (partBegPos > -1);
        }

        private int getLevel4Pos(int pos) 
        {
            return getLevel4Pos(pos, false);
        }

        private int getLevel4Pos(int pos, bool withOffset) 
        {
            int res = 0;
            foreach (SqlPart part in parts) 
            {
                int beg = withOffset ? part.Begin + part.BeginOffset : part.Begin;
                int end = withOffset ? part.End + part.EndOffset : part.End;
                if (pos >= beg && pos <= end)
                {
                    res = part.Level;
                    break;
                }
            }
            return res;
        }

        private SqlPart getSqlPart4Pos(int pos) 
        {
            return getSqlPart4Pos(pos, false);
        }

        private SqlPart getSqlPart4Pos(int pos, bool withOffset)
        {
            SqlPart res = null;
            foreach (SqlPart part in parts)
            {
                int beg = withOffset ? part.Begin + part.BeginOffset : part.Begin;
                int end = withOffset ? part.End + part.EndOffset : part.End;
                if (pos >= beg && pos <= end)
                {
                    res = part;
                    break;
                }
            }
            return res;
        }

        private SqlPart getSqlPartById(int id)
        {
            SqlPart res = null;
            foreach (SqlPart part in parts)
            {
                if (part.id == id)
                {
                    res = part;
                    break;
                }
            }
            return res;
        }

        private SqlPart getSqlPartEnd4Pos(int pos)
        {
            SqlPart res = null;
            SqlPart beg = getSqlPart4Pos(pos);
            if (beg != null)
            {
                int endPos = 0;
                foreach (SqlPart part in parts)
                {
                    if (part.id == beg.id)
                    {
                        if (part.End > endPos) 
                        {
                            res = part;
                            endPos = part.End;
                        }
                    }
                }
            }
            return res;
        }

        private int getBlockIdent4Pos(int pos) 
        {
            return getBlockIdent4Pos(pos, false);
        }

        private int getBlockIdent4Pos(int pos, bool withOffset)
        {
            int res = 0;
            foreach (SqlPart part in identBlocks)
            {
                int beg = withOffset ? part.Begin + part.BeginOffset : part.Begin;
                int end = withOffset ? part.End + part.EndOffset : part.End;
                if (pos >= beg && pos <= end)
                {
                    res += part.Level;
                }
            }
            return res;
        }

        private SqlKey getEnterKey4Key(string key) 
        {
            SqlKey res = null;
            foreach (SqlKey sqlkey in EnterKeys) 
            {
                if (sqlkey.Key == key) 
                {
                    res = sqlkey;
                    break;
                }
            }
            return res;
        }

        private void setSqlPartOffset(int pos, int offset) 
        {
            for (int i = 0; i < parts.Count; i++)
            {
                SqlPart part = parts[i];
                if (part.Begin >= pos)
                {
                    part.BeginOffset += offset;
                }
                if (part.End >= pos)
                {
                    part.EndOffset += offset;
                }
            }
        }

        private void setBlockIdentOffset(int pos, int offset)
        {
            for (int i = 0; i < identBlocks.Count; i++)
            {
                SqlPart part = identBlocks[i];
                if (part.Begin >= pos)
                {
                    part.BeginOffset += offset;
                }
                if (part.End >= pos)
                {
                    part.EndOffset += offset;
                }
            }
        }

        private void checkLineWidth() 
        {
            /* Розриви довгих рядків */
            int allLen = this.sql.Length;
            if (allLen > LineWidth)
            {
                int prevPos = 0;
                int commaIdx = 0;
                do
                {
                    int currPos = this.sql.IndexOf(BR, prevPos);
                    int lineLen = currPos - prevPos + 1;
                    while (lineLen > LineWidth)
                    {
                        commaIdx = this.sql.IndexOf(",", prevPos);
                        if (commaIdx > 0 && commaIdx < currPos)
                        {
                            int rangeLen = commaIdx - prevPos + 1;
                            while (rangeLen < LineWidth && commaIdx > 0 && commaIdx < currPos)
                            {
                                commaIdx = this.sql.IndexOf(",", commaIdx + 1);
                                rangeLen = commaIdx - prevPos + 1;
                            }

                            if (commaIdx > 0 && commaIdx < currPos && currPos - commaIdx + 1 >= LineWidthOverSize)
                            {
                                int newLinePos = commaIdx + 1;
                                int lvl = getLevel4Pos(newLinePos, true);
                                int blockIdents = getBlockIdent4Pos(newLinePos, true);
                                string insertStr = BRTAB + new String(CTAB, lvl) + new String(CTAB, blockIdents);
                                this.sql = this.sql.Insert(commaIdx + 1, insertStr);
                                prevPos = commaIdx + 1 + BRTAB.Length;
                                lineLen = currPos - prevPos + 1;
                            }
                            else 
                            {
                                lineLen = 0;
                            }
                        }
                        else 
                        {
                            lineLen = 0;
                        }
                    }
                    prevPos = currPos + 1;
                } while (prevPos > 0 && prevPos <= allLen);
            }
        }
        #endregion
    }
}