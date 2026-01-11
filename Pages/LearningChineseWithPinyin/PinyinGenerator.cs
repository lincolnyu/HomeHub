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

        private static string? SpecialCases(string subStr, int i, PinyinItem? pinyinItem)
        {
            var c = subStr[i];
            if (c == '尽')
            {
                if (i < subStr.Length - 1 && subStr[i + 1] == '管')
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
                if (i > 0 && subStr[i - 1] == '道' && pinyinItem is not null)
                {
                    return pinyinItem[1];
                }
            }
            else if (c == '缝')
            {
                if (pinyinItem is not null)
                {
                    if (i < subStr.Length - 1 && subStr[i + 1] == '纫')
                    {
                        return pinyinItem[0];
                    }
                    if (i < subStr.Length - 1 && subStr[i + 1] == '隙')
                    {
                        return pinyinItem[1];
                    }
                    if (i > 0 && (subStr[i - 1] == '条' || subStr[i - 1] == '丝' || subStr[i - 1] == '合'))
                    {
                        return pinyinItem[1];
                    }
                }
            }
            return null;
        }
    }
}
