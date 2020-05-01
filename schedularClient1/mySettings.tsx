//@ts-ignore
//Dee: react-native-dotenv doesn't play well with typechecking so we have to ignoire this
import {API_EP}  from 'react-native-dotenv';

const mySettings ={
    apiEndPoint:API_EP as string,
    dummyValueToForceAPI_EPtorefresh:21
};

//https://stackoverflow.com/questions/58270059/react-native-expo-environment-variables
//https://blog.expo.io/introducing-expo-release-channels-33dbe40ca400

export default mySettings;