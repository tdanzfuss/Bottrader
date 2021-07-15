#!/bin/sh

docker run --name bot_redis -p 6379:6379 -v /Users/tdanzfuss/Projects/Bottrader/redisData:/data -d redis redis-server --appendonly yes --requirepass R3tr0R@bb1t