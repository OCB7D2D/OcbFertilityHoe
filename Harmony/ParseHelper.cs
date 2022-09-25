using System;
using System.Collections.Generic;

namespace OCB
{
    public static class ParseHelper
    {
        public static Tuple<string, int>[] ParseResources(string args)
        {
            List<Tuple<string, int>> resources
                = new List<Tuple<string, int>>();
            foreach (var arg in args.Split(','))
            {
                if (string.IsNullOrWhiteSpace(arg)) continue;
                var pair = arg.Split(new char[] { ':' }, 2);
                int amount = pair.Length == 2 ? int.Parse(pair[1]) : 1;
                resources.Add(new Tuple<string, int>(pair[0].Trim(), amount));
            }
            return resources.ToArray();
        }

        public static ItemStack[] ConvertResources(Tuple<string, int>[] resources)
        {
            ItemStack[] stack = new ItemStack[resources.Length];
            for (int i = 0; i < resources.Length; i += 1)
                stack[i] = new ItemStack(ItemClass.GetItem(
                    resources[i].Item1), resources[i].Item2);
            return stack;
        }

        public static void ConvertResources(Tuple<string, int>[] resources, ref ItemStack[] stack)
        {
            if (stack != null) return;
            stack = ConvertResources(resources);
        }
    }
}
