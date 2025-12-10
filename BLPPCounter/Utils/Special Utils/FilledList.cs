using System.Collections.Generic;


namespace BLPPCounter.Utils.Special_Utils
{
    internal class FilledList
    {
        private readonly object Placeholder;
        public FilledListList List;
        public object Value;

        public bool HasValues => List[0] != Placeholder;

        internal FilledList(List<object> list = null, object value = null, object placeholder = null)
        {
            Placeholder = placeholder ?? "";
            List = list is null ? new FilledListList(this, Placeholder) : new FilledListList(this, list);
            Value = value ?? List[0];
        }
        internal class FilledListList: List<object>
        {
            private readonly FilledList FL;
            public FilledListList(FilledList fl, params object[] list) : base(list)
            {
                FL = fl;
            }
            public FilledListList(FilledList fL, List<object> list) : base(list)
            {
                FL = fL;
            }

            public new void Add(object item)
            {
                if (Count == 1 && this[0] == FL.Placeholder)
                {
                    this[0] = item;
                    FL.Value = item;
                }
                else base.Add(item);
            }
        }
    }
}
