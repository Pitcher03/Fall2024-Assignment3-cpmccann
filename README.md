# Fall2024-Assignment3-cpmccann

Notes on project 3:
Publishing was difficult. Visual Studio wanted me to log into a publish profile and I had no idea what the password is. I found it after looking at some posts for help.
Adding movies and actors takes a while. I used multiple queries instead of combining them in some parts, which is probably causing the inefficiency.
The openai resource is not great. A lot of times it will generate garbage. I tried to make it give all of the reviews / tweets in a single response separated by special characters, but it didn't always follow this format. To fix this I decided to see if the response was valid, and if not, it just ignores it. So sometimes when creating an actor or movie it will say "No reviews found!" because the chatgpt response was bad.
