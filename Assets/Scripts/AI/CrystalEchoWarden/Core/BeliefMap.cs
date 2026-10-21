using System;
namespace Skybreak.Warden.Core
{
    public sealed class BeliefMap
    {
        readonly double[] probabilities, scratch;
        public int Count=>probabilities.Length;
        public double this[int i]=>probabilities[i];
        public double LastEvidenceTime {get; private set;}=double.NegativeInfinity;
        public BeliefMap(int count)
        {
            if(count<1)throw new ArgumentOutOfRangeException(nameof(count));
            probabilities=new double[count]; scratch=new double[count]; Reset();
        }
        public void Reset()
        {
            for(int i=0;i<Count;i++)probabilities[i]=1.0/Count;
            LastEvidenceTime=double.NegativeInfinity;
        }
        public int MostLikely
        {
            get { int best=0; for(int i=1;i<Count;i++)if(probabilities[i]>probabilities[best])best=i; return best; }
        }
        public double Confidence(double now)
        {
            // Evidence age is distinct from a normalized spatial probability.
            return Math.Exp(-Math.Max(0,now-LastEvidenceTime)/6);
        }
        public double Concentration
        {
            get
            {
                if(Count==1)return 1;
                double entropy=0; foreach(double p in probabilities)if(p>0)entropy-=p*Math.Log(p);
                return Math.Max(0,1-entropy/Math.Log(Count));
            }
        }
        public void See(int node,double now)
        {
            if(node<0||node>=Count)return;
            double low=Count==1?0:.02/(Count-1);
            for(int i=0;i<Count;i++)probabilities[i]=i==node?(Count==1?1:.98):low;
            LastEvidenceTime=now;
        }
        public void Observe(double[] likelihood,double now)
        {
            if(likelihood==null||likelihood.Length!=Count)throw new ArgumentException("Likelihood size");
            for(int i=0;i<Count;i++)
            {
                if(!WorldGraph.Finite(likelihood[i])||likelihood[i]<0)throw new ArgumentException("Invalid likelihood");
                scratch[i]=probabilities[i]*Math.Max(1e-8,likelihood[i]);
            }
            double sum=0; foreach(var p in scratch)sum+=p;
            if(sum<=0)return;
            for(int i=0;i<Count;i++)probabilities[i]=scratch[i]/sum;
            LastEvidenceTime=now;
        }
        public void ClearVisible(int node)
        {
            if(node<0||node>=Count)return;
            probabilities[node]*=.05; Normalize();
        }
        public void Predict(WorldGraph graph,double dt)
        {
            if(graph.Count!=Count)throw new ArgumentException("Graph size changed");
            dt=Math.Max(0,Math.Min(dt,1)); Array.Clear(scratch,0,Count);
            for(int i=0;i<Count;i++)
            {
                int neighbours=0; foreach(var e in graph.Neighbours(i))if(e.Open)neighbours++;
                double moved=neighbours>0?Math.Min(.5,dt*.3):0;
                scratch[i]+=probabilities[i]*(1-moved);
                foreach(var e in graph.Neighbours(i))if(e.Open)scratch[e.To]+=probabilities[i]*moved/neighbours;
            }
            double fade=Math.Min(.1,dt*.02);
            for(int i=0;i<Count;i++)probabilities[i]=scratch[i]*(1-fade)+fade/Count;
            Normalize();
        }
        void Normalize()
        {
            double total=0; foreach(double p in probabilities)total+=p;
            for(int i=0;i<Count;i++)probabilities[i]=total>1e-12?probabilities[i]/total:1.0/Count;
        }
        public static double[] SoundLikelihood(double[] loss,double loudness,double heard,double sigma=1.5)
        {
            if(sigma<=0)throw new ArgumentOutOfRangeException(nameof(sigma));
            var likelihood=new double[loss.Length];
            for(int i=0;i<loss.Length;i++)
            {
                // Gaussian observation model with background-noise floor.
                double expected=Math.Max(0,loudness-loss[i]);
                double delta=expected-heard;
                likelihood[i]=.01+.99*Math.Exp(-delta*delta/(2*sigma*sigma));
            }
            return likelihood;
        }
    }
}
