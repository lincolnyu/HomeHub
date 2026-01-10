using System.Text;
using hyjiacan.py4n;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomeHubApp.Pages.LearningChineseWithPinyin
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public required string InputText { get; set; }

        public required string OutputText { get; set; }

        public void OnGet()
        {
            // Initial page load - no action needed
        }

        public void OnPost()
        {
            if (!string.IsNullOrEmpty(InputText))
            {
                // Placeholder: Call your conversion logic here (implement the method below or replace with your library/program call)
                OutputText = ConvertToPinyin(InputText, 70, (16,-1));
            }
        }

        private string ConvertToPinyin(string input, int? lineCharacterLimit = null, (int,int)? adjuster = null)
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

                int count = 0;  // actually it's just lengh of total pinyin
                int adjustCount = 0;
                int totalPyAdjusted = 0;
                for (var i = 0; i < line.Length; i++)
                {
                    if (adjuster.HasValue)
                    {
                        if (adjuster.Value.Item2 > 0)
                        {
                            if (adjuster.Value.Item1 * (adjustCount+1) <= count - totalPyAdjusted)
                            {
                                pinyinSb.Append(new string(' ', adjuster.Value.Item2));
                                count += adjuster.Value.Item2;
                                adjustCount++;
                                totalPyAdjusted += adjuster.Value.Item2;
                            }
                        }
                        else if (adjuster.Value.Item2 < 0)
                        {
                            if (adjuster.Value.Item1 * (adjustCount+1) <= count)
                            {
                                charSb.Append(new string(' ', -adjuster.Value.Item2));
                                count += -adjuster.Value.Item2;
                                adjustCount++;
                            }
                        }
                    }

                    char c = line[i];
                    if (PinyinUtil.IsHanzi(c))
                    {
                        // Assuming the minimum pinyin length is 2
                        string py = Pinyin4Net.GetFirstPinyin(c, format);
                        pinyinSb.Append(py);
                        pinyinSb.Append(' ');
                        charSb.Append(c);
                        charSb.Append(new string(' ', py.Length-1));
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
                            if (i+1<line.Length && PinyinUtil.IsHanzi(line[i+1]))
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
    }
}