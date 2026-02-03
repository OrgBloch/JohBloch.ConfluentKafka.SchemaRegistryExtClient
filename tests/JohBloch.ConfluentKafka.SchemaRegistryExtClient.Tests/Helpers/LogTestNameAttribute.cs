using System;
using System.Reflection;
using Xunit.Sdk;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Tests.Helpers
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class LogTestNameAttribute : BeforeAfterTestAttribute
    {
        public override void Before(MethodInfo methodUnderTest)
        {
            Console.WriteLine($"--- Starting test: {methodUnderTest.DeclaringType?.FullName}.{methodUnderTest.Name} ---");
        }

        public override void After(MethodInfo methodUnderTest)
        {
            // Intentionally left blank; could log test end here if desired.
        }
    }
}