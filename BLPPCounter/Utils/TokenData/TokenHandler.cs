using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Utils.TokenParser.Printers;
using System;
using System.Collections.Generic;

namespace BLPPCounter.Utils.TokenData
{
    internal class TokenHandler(FormatRelation relator, FormatWrapper wrapper, Func<string, FormatWrapper, Dictionary<string, char>, Formatter> formatCreator)
    {
        private readonly FormatRelation relator = relator;
        private readonly FormatWrapper wrapper = wrapper;
        private readonly Func<string, FormatWrapper, Dictionary<string, char>, Formatter> formatCreator = formatCreator;

        private Formatter formatter;
        private Printer printer;

        public Printer Printer => printer;

        public void UpdateFormat(string format)
        {
            formatter = formatCreator(format, wrapper, relator.Alias);
            printer = formatter.GetOutput();
        }
    }
}
