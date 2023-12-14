CREATE TABLE OTHER_PROJECT_ID.OTHER_DATASET_ID.channels_admin_log (
	ChannelId INT64,
	EventId INT64,
	UserId INT64,
	Action STRING,
	InviteLink STRING,
	InviteLinkName STRING,
	Date DATETIME,
	AdminId INT64
);

CREATE TABLE OTHER_PROJECT_ID.OTHER_DATASET_ID.channels_general_information (
	ChannelId INT64,
	SubscribersCount INT64,
	Date DATETIME
);

CREATE TABLE OTHER_PROJECT_ID.OTHER_DATASET_ID.channels_messages (
	ChannelId INT64,
	MessageId INT64,
	Date TIMESTAMP,
	Views INT64,
	Forwards INT64,
	Reactions INT64,
	ReactionsFull STRING,
	Er FLOAT64,
	Err FLOAT64,
	TimeNow TIMESTAMP
);

CREATE TABLE OTHER_PROJECT_ID.OTHER_DATASET_ID.channels_users (
	ChannelId INT64,
	UserId INT64,
	TimeNow DATETIME
);
