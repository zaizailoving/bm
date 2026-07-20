using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BM.Service.Core.Models
{
    /// <summary>
    /// select items model
    /// </summary>
    public class FormSelectItem
    {
        /// <summary>
        /// key
        /// </summary>
        public string code { get; set; } = string.Empty;
        /// <summary>
        /// comment
        /// </summary>
        public string comments { get; set; } = string.Empty;

        /// <summary>
        /// text
        /// </summary>
        public string name { get; set; } = string.Empty;
        /// <summary>
        /// value
        /// </summary>
        public string value { get; set; } = string.Empty;
    }
}
