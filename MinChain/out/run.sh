#!/bin/sh

dotnet MinChain.dll genkey > mykey.json
dotnet MinChain.dll config > config.json
sed -i -e 's/<YOUR OWN KEYPAIR>/mykey/g' config.json
sed -i -e 's/<GENESIS BLOCK>/genesis/g' config.json
sed -i -e '/127\.0\.0\.1:9333/d' config.json

echo Starting...
dotnet MinChain.dll run config.json
