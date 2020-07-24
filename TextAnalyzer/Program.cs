using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;

namespace TextAnalyzer
{
    class Program
    {
        /// <summary>
        /// The conjunctions
        /// </summary>
        /// <remarks>
        /// I am only using coordinating conjunctions here as I cannot find a full list of 
        /// english conjunctions.
        /// </remarks>
        private static readonly string[] conjunctions = new string[] 
        {
                "and", "but", "for", "nor", "or", "so", "yet"
        };

        static void Main(string[] args)
        {
            bool lookingForFile = false;
            string search = null;
            string filename = "A Tale of Two Cities - Charles Dickens.txt";
            bool excludeConjunctions = true;
            int? topX = null;

            foreach (string arg in args)
            {
                if (!lookingForFile && !arg.StartsWith("-"))
                {
                    if (int.TryParse(arg, out int tmp))
                        topX = tmp;
                    else
                        search = arg;
                }
                else if (lookingForFile)
                {
                    filename = arg;
                    lookingForFile = false;
                }
                else if (arg.ToLower() == "-f" || arg.ToLower() == "--file")
                    lookingForFile = true;
                else if (arg.ToLower() == "-i" || arg.ToLower() == "--include")
                    excludeConjunctions = false;
                else if (arg.ToLower() == "-u" || arg.ToLower() == "--usage")
                    Console.WriteLine(@"
usage: TextAnalyzer [-u | --usage] [-f | --file] <filename> [-i | --include] 
                    <search term> 
                    <top amount>

-u | --usage            : Displays this text
-f | --file <filename>  : Selects a different file than the default
-i | --include          : Includes the conjunctions in the top results
<search term>           : If a string argument cannot be parsed as an 
                          integer it will generate a matches.txt file 
                          that contains the lines where the search term is 
                          found separated by commas
<top amount>            : If an integer argument is found a topwords.txt file
                          will be generated with that amount of the top words 
                          and their counts.
");
            }

            try
            {
                string fileData = File.ReadAllText(filename);
                var byteCount = GetByteCounts(fileData);
                File.WriteAllText("byteCount.txt", WordCountDisplay(byteCount));
                var punctCount = GetPunctuationCounts(fileData);
                File.WriteAllText("punctCount.txt", WordCountDisplay(punctCount));
                var wordCount = GetWordCounts(fileData);
                File.WriteAllText("wordCount.txt", WordCountDisplay(wordCount));
                if (!string.IsNullOrEmpty(search))
                {
                    var matches = GetMatches(fileData, search);
                    File.WriteAllText("matches.txt", string.Join(", ", matches));
                }
                if (topX != null)
                {
                    var topWords = GetTopWords(wordCount, topX.Value, excludeConjunctions);
                    File.WriteAllText("topwords.txt", WordCountDisplay(topWords));
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to open file");
            }
        }

        private static string WordCountDisplay(IEnumerable<WordCountItem> byteCount)
        {
            var sb = new StringBuilder();
            foreach (var item in byteCount)
            {
                switch (item.Word)
                {
                    case "\r":
                        sb.AppendFormat("'carriage return' : {0}", item.Count);
                        break;
                    case "\n":
                        sb.AppendFormat("'new line' : {0}", item.Count);
                        break;
                    case " ":
                        sb.AppendFormat("'space' : {0}", item.Count);
                        break;
                    default:
                        sb.AppendFormat("{0} : {1}", item.Word, item.Count);
                        break;
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static IEnumerable<WordCountItem> GetByteCounts(string fileData)
        {
            return fileData.
                GroupBy(c => c).
                Select(g => new WordCountItem
                {
                    Word = g.Key.ToString(),
                    Count = g.Count()
                }).
                OrderBy(g => g.Count);
        }

        private static IEnumerable<WordCountItem> GetPunctuationCounts(string fileData)
        {
            var pattern = new Regex(@"[^\w\d\s]");
            return fileData.
                Where(c => pattern.IsMatch(c.ToString())).
                GroupBy(c => c).
                Select(g => new WordCountItem 
                { 
                    Word = g.Key.ToString(), 
                    Count = g.Count() 
                }).
                OrderByDescending(g => g.Count);
        }

        private static IEnumerable<WordCountItem> GetWordCounts(string fileData)
        {
            // Words that have a ’ character are contractions and still count as words
            // I am excluding words that begin or end with a single quote as that includes
            // words at the beginning and end of quotes inside dialog.
            // Using [a-zA-Z] instead of \w because in a literary sense words do not contain
            // underscores or numbers.
            // This scheme does include roman numerals but I feel that is acceptable.
            var pattern = new Regex(@"([a-zA-Z]+’?[a-zA-Z]+)");
            return pattern.
                Matches(fileData).
                Select(m => m.Value).
                GroupBy(m => m.ToLower()).
                Select(g => new WordCountItem 
                { 
                    Word = g.Key, 
                    Count = g.Count() 
                }).
                OrderBy(g => g.Word);
        }

        private static IEnumerable<int>GetMatches(string fileData, string searchTerm)
        {
            var lines = fileData.Split("\n");
            List<int> matches = new List<int>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    matches.Add(i + 1);
            }
            return matches;
        }

        private static IEnumerable<WordCountItem> GetTopWords(IEnumerable<WordCountItem> words, int topX, bool exclude)
        {
            IQueryable<WordCountItem> query = words.AsQueryable();
            if (exclude)
                query = query.Where(i => !conjunctions.Contains(i.Word));
            return query.OrderByDescending(i => i.Count).Take(topX);
        }
    }

    struct WordCountItem
    {
        public string Word { get; set; }
        public int Count { get; set; }
    }
}
