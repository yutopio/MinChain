#!/bin/sh

tr -d '\r' < MinChain/out/run.sh > MinChain/out/run.sh.temp
mv MinChain/out/run.sh.temp MinChain/out/run.sh

docker build .
