import React from 'react';
import { Text, View, Button, ActivityIndicator, FlatList  } from "react-native";
import mySettings from '../mySettings';
import {useDataFetching} from '../components/DataLoader';
import {siteColors,siteStyles,fromUTCDate} from '../constants/styles';
import {JobInfoModel} from '../generated/JobInfoModel';

import {screenRouteProps} from '../components/siteNavigation';

//dee todo change so that this object doesn't need to know how its routed
export default function({navigation}:screenRouteProps<'Home'>) {
    const { loading, results, error } = 
            useDataFetching<JobInfoModel[]>(`${mySettings.apiEndPoint}jobs/list`);

    if(loading){
        return <ActivityIndicator style={[siteStyles.centered]} 
                animating={true} color={siteColors.tintColor} size="large"
            />;
    }
    
    if(error || !results){
        return <Text style={[siteStyles.danger,siteStyles.centered]}>
                {error||'no data loaded'}
        </Text>;
    }

    return <View style={{flex:1,alignItems:"center"}} >
        <Text style={[{fontWeight:"bold"}]}>
        Current Jobs
        </Text>

        <FlatList 
            contentContainerStyle={{
                flexGrow: 1,
                justifyContent:"flex-start"
                }}

            data={results} keyExtractor={job=>job.jobName}
            renderItem={({item})=>{
                const {jobName, description,isRunning,nextScheduled,previousFired} = item;
                return <View style={siteStyles.Card} >
                    <Text>{jobName}:{description}</Text>
                    
                    <Text>Next Scheduled :{fromUTCDate(nextScheduled)}</Text>
                    <Text>Last ran @ {fromUTCDate(previousFired)} </Text>
                    <Button title="show details" 
                        onPress={() => {
                        //console.log('You click by View');
                        navigation.navigate('Details',{jobName});
                    }}/>
                </View>;
            }}
        />
            
    </View>
};



