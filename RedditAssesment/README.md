# Prerequisites

Reddit access token. If you do not have one you can aquire it from [https://not-an-aardvark.github.io/reddit-oauth-helper/](https://not-an-aardvark.github.io/reddit-oauth-helper/) Source code: [(github)](https://github.com/not-an-aardvark/reddit-oauth-helper?tab=readme-ov-file)

A yaml configuration file with the structure depited in the example file `config.yml`

# How to Run

You can either run the project through a debugger, for example Visual Studio, or you can build it to an executable.

There is a command line argument `--config/-c` which defaults to `./config.yml`. You can specfiy here your configuration file.

# What this does

This small program polls reddit's api to get post information from configured subreddits. The data is then aggregated into some statistics:
- Most liked posts
- Users with the most posts
- Newest posts
- Most popular hours