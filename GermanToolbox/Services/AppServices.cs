using Microsoft.Extensions.DependencyInjection;

namespace GermanToolbox
{
    public static class AppServices
    {
        public static IServiceProvider Current { get; set; } =
            new ServiceCollection().BuildServiceProvider();

        public static T GetRequiredService<T>() where T : notnull =>
            Current.GetRequiredService<T>();
    }
}
