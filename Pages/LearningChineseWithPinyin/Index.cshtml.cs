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
                OutputText = ConvertToPinyin(InputText, 80);
            }
        }

        private string ConvertToPinyin(string input, int? lineCharacterLimit = null)
        {
            PinyinFormat format = PinyinFormat.WITH_TONE_MARK | PinyinFormat.LOWERCASE | PinyinFormat.WITH_U_UNICODE;
            using var sr = new StringReader(input);
            var sw = new StringBuilder();
            while (true)
            {
                string? line = sr.ReadLine();
                if (line == null)
                    break;

                StringBuilder pinyinSb = new StringBuilder();
                StringBuilder charSb = new StringBuilder();

                int count = 0;
                for (var i = 0;i < line.Length; i++)
                {
                    char c = line[i];
                    if (PinyinUtil.IsHanzi(c))
                    {
                        string py = Pinyin4Net.GetFirstPinyin(c, format);
                        pinyinSb.Append(py);
                        pinyinSb.Append(' ');
                        charSb.Append(c);
                        charSb.Append(new string(' ', py.Length-1));
                        count += py.Length + 1;
                    }
                    else
                    {
                        pinyinSb.Append(c);
                        charSb.Append(c);
                        
                        // TODO it could be 1-letter wide
                        count += 2;
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