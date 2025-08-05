using ScriptsOfTribute.AI;
using ScriptsOfTribute.Board;
using ScriptsOfTribute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BenchmarkingUtility
{
    public class Result
    {
        public (AI, AI) Competitors;
        public List<PatronId> Patrons;
        public AI Winner;
        public AI Looser;
        public GameEndReason GameEndReason;
    }
}
