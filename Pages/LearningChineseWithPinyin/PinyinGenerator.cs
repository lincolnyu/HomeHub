using hyjiacan.py4n;
using System.Text;

namespace HomeHubApp.Pages.LearningChineseWithPinyin
{
    public class PinyinGenerator
    {
        public static string ConvertToPinyin(string input, int? lineCharacterLimit = null, (int, int)? adjuster = null)
        {
            PinyinFormat format = PinyinFormat.WITH_TONE_MARK | PinyinFormat.LOWERCASE | PinyinFormat.WITH_U_UNICODE;
            using var sr = new StringReader(input);
            var sw = new StringBuilder();
            while (true)
            {
                string? line = sr.ReadLine();
                if (line == null)
                    break;

                (line, var overridingDict) = PreprocessPinyinAnnotations(line);

                StringBuilder pinyinSb = new();
                StringBuilder charSb = new();

                //PinyinItem[] pinyinItems = new PinyinItem[line.Length];
                string[] selectedPinyins = new string[line.Length];
                {
                    // Pre-process
                    int? currentStart = null;
                    for (var i = 0; i < line.Length + 1; i++)
                    {
                        if (i < line.Length && PinyinUtil.IsHanzi(line[i]))
                        {
                            if (currentStart == null)
                            {
                                currentStart = i;
                            }
                        }
                        else
                        {
                            if (currentStart is not null)
                            {
                                var subStr = line[(int)currentStart..i];
                                var pinyinArray = Pinyin4Net.GetPinyinArray(subStr, format);
                                for (var j = 0; j < subStr.Length; j++)
                                {
                                    if (overridingDict.TryGetValue(currentStart.Value + j, out var overridingIndex))
                                    {
                                        overridingIndex = overridingIndex < 0? 0 : overridingIndex < pinyinArray[j].Count ? overridingIndex : pinyinArray[j].Count-1;
                                        if (j < pinyinArray.Count)
                                        {
                                            selectedPinyins[currentStart.Value + j] = pinyinArray[j][overridingIndex];
                                        }
                                    }
                                    else
                                    {
                                        var specialPinyin = SpecialCases(subStr, j, j < pinyinArray.Count? pinyinArray[j] : null);
                                        if (specialPinyin != null)
                                        {
                                            selectedPinyins[currentStart.Value + j] = specialPinyin;
                                        }
                                        else
                                        {
                                            if (j < pinyinArray.Count)
                                            {
                                                selectedPinyins[currentStart.Value + j] = pinyinArray[j][0];
                                            }
                                        }
                                    }
                                }
                            }

                            currentStart = null;
                        }
                    }
                }

                int count = 0;  // actually it's just lengh of total pinyin
                int adjustCount = 0;
                int totalPyAdjusted = 0;

                for (var i = 0; i < line.Length; i++)
                {
                    if (adjuster.HasValue)
                    {
                        if (adjuster.Value.Item2 > 0)
                        {
                            if (adjuster.Value.Item1 * (adjustCount + 1) <= count - totalPyAdjusted)
                            {
                                pinyinSb.Append(new string(' ', adjuster.Value.Item2));
                                count += adjuster.Value.Item2;
                                adjustCount++;
                                totalPyAdjusted += adjuster.Value.Item2;
                            }
                        }
                        else if (adjuster.Value.Item2 < 0)
                        {
                            if (adjuster.Value.Item1 * (adjustCount + 1) <= count)
                            {
                                charSb.Append(new string(' ', -adjuster.Value.Item2));
                                count += -adjuster.Value.Item2;
                                adjustCount++;
                            }
                        }
                    }

                    char c = line[i];
                    if (selectedPinyins[i] != null)
                    {
                        string py = selectedPinyins[i];  //  Pinyin4Net.GetFirstPinyin(c, format);

                        // Assuming the minimum pinyPinyinin length is 2
                        pinyinSb.Append(py);
                        pinyinSb.Append(' ');
                        charSb.Append(c);
                        charSb.Append(new string(' ', py.Length - 1));
                        count += py.Length + 1;
                    }
                    else
                    {
                        if (c < 256)
                        {
                            pinyinSb.Append(" ");//instead of pinyinSb.Append(c);
                            charSb.Append(c);
                            count += 1;
                        }
                        else
                        {
                            pinyinSb.Append("  ");//full-width space
                            charSb.Append(c);
                            count += 2;
                        }
                    }

                    if (lineCharacterLimit.HasValue)
                    {
                        if (c == '。' && count > 0.6 * lineCharacterLimit.Value)
                        {
                            sw.AppendLine(pinyinSb.ToString());
                            sw.AppendLine(charSb.ToString());
                            pinyinSb.Clear();
                            charSb.Clear();
                            count = 0;
                            adjustCount = 0;
                            totalPyAdjusted = 0;
                        }
                        else if (count >= lineCharacterLimit.Value)
                        {
                            if (i + 1 < line.Length && PinyinUtil.IsHanzi(line[i + 1]))
                            {
                                sw.AppendLine(pinyinSb.ToString());
                                sw.AppendLine(charSb.ToString());
                                pinyinSb.Clear();
                                charSb.Clear();
                                count = 0;
                                adjustCount = 0;
                                totalPyAdjusted = 0;
                            }
                        }
                    }
                }

                sw.AppendLine(pinyinSb.ToString());
                sw.AppendLine(charSb.ToString());
            }
            return sw.ToString();
        }

        private static (string, Dictionary<int,int>) PreprocessPinyinAnnotations(string line)
        {
            Dictionary<int,int> map = [];
            var sb = new StringBuilder();
            var sbNum = new StringBuilder();
            int? charLocation = null;

            foreach (var c  in line)
            {
                if (charLocation is not null)
                {
                    if (c is ')')
                    {
                        if(int.TryParse(sbNum.ToString(), out var index))
                        {
                            map[charLocation.Value] = index;
                        }
                        charLocation = null;
                    }
                    else if (char.IsDigit(c))
                    {
                        sbNum.Append(c);
                    }
                }
                else if (c is '(' && sb.Length > 0)
                {
                    charLocation = sb.Length-1;
                }
                else
                {
                    sb.Append(c);
                }
            }
            return (sb.ToString(), map);
        }

        private static string? SpecialCases(string subStr, int i, PinyinItem? pinyinItem)
        {
            var c = subStr[i];
            if (c == '还')
            {
                if (pinyinItem is not null)
                {
                    if (IsInContext(subStr, i, "还债", "还钱", "还情", "还愿", "还原", "还乡", "还了", "还给", "归还"))
                    {
                        return pinyinItem[1];
                    }
                    if (i == subStr.Length-1)
                    {
                        return pinyinItem[1];
                    }
                }
            }
            else if (c == '干')
            {
                if (pinyinItem is not null)
                {
                    if (IsInContext(subStr, i, "干什么", "干啥", "干部", "干事", "干成", "干大", "干小", "干校"))
                    {
                        return pinyinItem[1];
                    }
                    if (i == subStr.Length - 1)
                    {
                        return pinyinItem[1];
                    }
                }
            }
            else if (c == '尽')
            {
                if (IsInContext(subStr, i, "尽管"))
                {
                    return "jǐn";
                }
                else
                {
                    return "jìn";
                }
            }
            else if (c == '观')
            {
                if (IsInContext(subStr, i, "道观") && pinyinItem is not null)
                {
                    return pinyinItem[1];
                }
            }
            else if (c == '缝')
            {
                if (pinyinItem is not null)
                {
                    if (IsInContext(subStr, i, "缝纫"))
                    {
                        return pinyinItem[0];
                    }
                    if (IsInContext(subStr, i, "缝隙", "合缝", "条缝", "丝缝"))
                    {
                        return pinyinItem[1];
                    }
                }
            }
            else if (c == '种')
            {
                if (pinyinItem is not null)
                {
                    if (IsInContext(subStr, i, "一种", "有种"))
                    {
                        return pinyinItem[0];
                    }
                    if (IsInContext(subStr, i, "种菜", "种地", "种田", "种树"))
                    {
                        return pinyinItem[2];
                    }
                    if (IsInContext(subStr, i, "种一粒"))
                    {
                        return pinyinItem[2];
                    }
                    if (IsInContext(subStr, i, "老种经略", "小种经略"))
                    {
                        return pinyinItem[1];
                    }
                }
            }
            else if (c == '血')
            {
                if (pinyinItem is not null)
                {
                    if (IsInContext(subStr, i, "用血", "血淋淋", "一滴血", "一针见血"))
                    {
                        return pinyinItem[1];
                    }
                    if (IsEnding(subStr, i, "血了") || i == subStr.Length - 1)
                    {
                        return pinyinItem[1];
                    }
                }
            }
            else if (c == '子')
            {
                if (pinyinItem is not null)
                {
                    if (IsInContext(subStr, i, "颗子", "粒子", "女子", "孔子"))
                    {
                        return pinyinItem[1];
                    }
                }
            }
            else if (c == '处')
            {
                if (pinyinItem is not null)
                {
                    if (IsInContext(subStr, i, "处在", "处于", "处女", "处子", "处境", "相处"))
                    {
                        return pinyinItem[0];
                    }
                    else
                    {
                        return pinyinItem[1];
                    }
                }
            }
            return null;
        }

        private static bool IsInContext(string subStr, int i, string context)
        {
            var c = subStr[i];
            var iInContext = context.IndexOf(c);
            if (i < iInContext)
            {
                return false;
            }
            if (subStr.Length - i < context.Length - iInContext)
            {
                return false;
            }
            return subStr[(i - iInContext)..(i - iInContext + context.Length)] == context;
        }

        private static bool IsEnding(string subStr, int i, string context)
        {
            var c = subStr[i];
            var iInContext = context.IndexOf(c);
            if (i < iInContext)
            {
                return false;
            }
            if (subStr.Length - i != context.Length - iInContext)
            {
                return false;
            }
            return subStr[(i - iInContext)..(i - iInContext + context.Length)] == context;
        }

        private static bool IsInContext(string subStr, int i, params string[] contexts)
        {
            return contexts.Any(x=> IsInContext(subStr, i, x));
        }
    }
}
