import React from 'react';
import {Button,Text,View} from 'react-native';
import { RouteProp } from '@react-navigation/native';
import { StackNavigationProp } from '@react-navigation/stack';
import { createStackNavigator } from '@react-navigation/stack';

import ListofJobView from '../screens/jobList';
import Details from '../screens/jobDetails';

const Stack = createStackNavigator();

export type allRoutes = {
    Home: undefined;
    Details: { jobName: string };
};

export type screenRouteProps<T extends keyof allRoutes> = {
    route: RouteProp<allRoutes, T>;
    navigation:  StackNavigationProp<allRoutes,T>;
}

function HomeButton(navigation:any){
  return <View style={{marginRight:10}}>
    <Button 
      color="#ccc"
      title='home'
      onPress={()=>navigation.navigate('Home')}/>
    </View>;
}

export function SiteNavigation(){
    return <Stack.Navigator initialRouteName="Home">
      <Stack.Screen name="Home" component={ListofJobView} />
      <Stack.Screen name="Details" component={Details} options={({ navigation, route }) => ({
          headerRight:() => {
          return <HomeButton {...navigation}/>;
        }
        })}/>
    </Stack.Navigator>
} 