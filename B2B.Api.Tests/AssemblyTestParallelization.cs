using Xunit;

// CustomWebApplicationFactory migrates a fixed LocalDB name; parallel test classes race on startup.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
