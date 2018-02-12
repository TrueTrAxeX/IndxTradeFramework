Пример использования:

Credentials TraderCredentials = new Credentials()
{
	TraderLogin = "6i18qz0BcfdFqj",
	TraderPassword = "H4weqYsVh7kNpgYRvXFRWzDGZatseaDW3js",
	Wmid = "54412415665"
};

var indxApi = new IndxTradeApi(TraderCredentials);
var indxClient = new IndxSiteClient();

С помощью этих двух классов вы сможете работать с сервисом Indx.ru. Первый класс предоставляет доступ к авторизованным функциям, второй работает без авторизации.