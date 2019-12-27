# Pubbie

[![feedz.io](https://img.shields.io/badge/endpoint.svg?url=https%3A%2F%2Ff.feedz.io%2Fdavidfowl%2Fpubbie%2Fshield%2FPubbie%2Flatest&label=Pubbie)](https://f.feedz.io/davidfowl/pubbie/packages/Pubbie/latest/download)

Pubbie is a high performance volatile pub/sub implementation based on [Bedrock.Framework](https://github.com/davidfowl/BedrockFramework).


## Using CI builds

To use CI builds add the following nuget feed:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <clear />
        <add key="bedrockframework" value="https://f.feedz.io/davidfowl/bedrockframework/nuget/index.json" />
        <add key="pubbie" value="https://f.feedz.io/davidfowl/pubbie/nuget/index.json" />
        <add key="NuGet.org" value="https://api.nuget.org/v3/index.json" />
    </packageSources>
</configuration>
```
