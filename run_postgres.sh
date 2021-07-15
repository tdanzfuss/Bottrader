#!/bin/sh

docker run --name bot_postgres -p 5432:5432 -e POSTGRES_PASSWORD=R3tr0R@bb1t -v /Users/tdanzfuss/Projects/Bottrader/postgresData:/var/lib/postgresql/data  -v /Users/tdanzfuss/Projects/Bottrader/postgresInit:/docker-entrypoint-initdb.d -d postgres