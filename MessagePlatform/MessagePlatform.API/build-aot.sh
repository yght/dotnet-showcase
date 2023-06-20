#!/bin/bash

echo "Building with Native AOT compilation..."

# clean previous builds
dotnet clean

# restore packages
dotnet restore

# publish with AOT
dotnet publish -c Release -r linux-x64 --self-contained

echo "AOT build completed!"
echo ""
echo "Benefits of this build:"
echo "- Faster startup time (typically < 50ms)"  
echo "- Lower memory usage (often 50% reduction)"
echo "- No JIT compilation overhead"
echo "- Single file executable"
echo "- Better performance for serverless scenarios"
echo ""
echo "Trade-offs:"
echo "- Longer build time"
echo "- Larger executable size"
echo "- Some reflection limitations"
echo "- Not all libraries are AOT compatible"