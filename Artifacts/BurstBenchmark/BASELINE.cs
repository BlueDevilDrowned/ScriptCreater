using Unity.Burst;
using UnityEngine;

public class BurstTest:MonoBehaviour
{
    public long num=1000;
    public bool starttest=false;
    public bool startburst=false;
    public bool startAll=false;
    public void Update()
    {
        if(startAll)
        {
            starttest=true;
            startburst=true;
            startAll=false;
        }
        if(starttest)
        {

            
            float time=Time.unscaledTime;
            test();
            Debug.Log("Close Busrt for"+num+"need:"+(Time.unscaledTime-time));
            starttest=false;
        }
        if(startburst)
        {
            float time=Time.unscaledTime;
            test1();
            Debug.Log("Open Busrt for"+num+"need:"+(Time.unscaledTime-time));
            startburst=false;
        }
    }
    public void test()
    {
        for(long i=0;i<num;i++)
        {
            
        } 
    }
    [BurstCompile]
    public void test1()
    {
        for(long i=0;i<num;i++)
        {
            
        } 
    }
}
