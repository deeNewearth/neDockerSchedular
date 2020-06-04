This is for  deployment where neschedular will be running behind nginx proxy 
With basic security provided by htpasswd

To create password 

https://docs.nginx.com/nginx/admin-guide/security-controls/configuring-http-basic-authentication/

we create password files by creating a tmp docker container. in the deployment box cd to The deployment folder and then
docker run -it --rm -v $PWD:/config   httpd bash
htpasswd -c /config/.htpasswd revsupport
to add more
htpasswd /config/.htpasswd jay
htpasswd -c /.htpasswd dee