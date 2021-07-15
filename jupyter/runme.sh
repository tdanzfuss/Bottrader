#!/bin/bash  
docker run --rm -p 10000:8888 -e JUPYTER_ENABLE_LAB=yes -v /Users/tdanzfuss/Projects/Bottrader/jupyter/:/home/jovyan/work tdanzfuss/sense_play