import React, { useState } from 'react';
import { Text, View, Button, ActivityIndicator, 
    StyleSheet, FlatList   } from "react-native";
import mySettings from '../mySettings';
import {useDataFetching,usePrevious} from '../components/DataLoader';
import {siteColors,siteStyles,fromUTCDate} from '../constants/styles';
import {JobRunningStatusModel} from '../generated/JobRunningStatusModel';

import {screenRouteProps} from '../components/siteNavigation';

export default function({navigation, route}:screenRouteProps<'Details'>) {

    try{
        const {jobName} = route.params;
        if(!jobName)
            throw 'details screen needs a job Name';
        
        const [loadedTime,setLoadedTime] = useState(new Date().toLocaleString());
        const [runTriggeredTime,setRunTriggeredTime] = useState('');

        const lastRunTriggeredTime = usePrevious(runTriggeredTime);

        let fetchUrl =`${mySettings.apiEndPoint}jobs/status/${encodeURIComponent(jobName)}?`
            +  `loaded=${encodeURIComponent(loadedTime)}`;

        if(runTriggeredTime && lastRunTriggeredTime!=runTriggeredTime){
            console.log('run now trigerred');
            fetchUrl+='&runNow=true';
        }
            

        const { loading, results, error } = useDataFetching<JobRunningStatusModel>(fetchUrl);
        
        if(error) throw error;

        if(loading){
            return <ActivityIndicator style={[siteStyles.centered]} 
                    animating={true} color={siteColors.tintColor} size="large"
                />;
        }

        if(!results) throw 'no data found';

        const {info,logs} = results;
        const {description,isRunning,nextScheduled,previousFired} = info;

        return <View style={{flex:1,margin:5}}>
            <View style={[{alignItems:"center"}]}>
                <View style={[siteStyles.Card, styles.statusBlock]}>

                    <View style={styles.itemTwocolumn}>
                        <Text style={{fontWeight:"bold"}}>{jobName}:{description}</Text>
                        <Text>prev Ran :{fromUTCDate(previousFired)}</Text>
                        <Text>next Scheduled :{fromUTCDate(nextScheduled)}</Text>
                    </View>

                    <View style={[styles.itemTwocolumn,isRunning?siteStyles.warning:{}]}>
                        {isRunning?<Text >
                            Task is running: started at {previousFired} UTC
                        </Text>
                            :<View>
                                <Text style={[siteStyles.centered]}>NOT running</Text>
                                <Button  title="Run now"
                                    color={siteColors.warningBackground}
                                    onPress={()=>setRunTriggeredTime(new Date().toLocaleString())}
                                />
                                {lastRunTriggeredTime? <Text>last ran @ {lastRunTriggeredTime}</Text>:null}
                            </View>
                            }
                    </View>

                </View>
            </View>

            <View style={{flexDirection:"row",justifyContent:"space-between"}}>
                
                <View>
                    <Text style={{marginTop:5}}>Logs</Text>
                    <Text style={{fontSize:9,fontWeight:"400"}}>@{loadedTime}</Text>
                </View>
                
                <Button title="refresh logs" onPress={()=>setLoadedTime(new Date().toLocaleString())}/>
            </View>
            

            <View style={siteStyles.separator} />

            <View style={{flex:1}}>
                <FlatList

                    contentContainerStyle={{
                        flexGrow: 1,
                        }}

                    data={logs||[]}
                    keyExtractor={(x, i) => i.toString()}
                    renderItem={({item})=>{
                        const it = JSON.parse(item);
                        let style={};
                        switch(it["@l"]){
                            case 'Fatal':style= siteStyles.danger;break;
                            case 'Warning':case 'Warn':style= siteStyles.warning;break;
                        }

                        const logTime =new Date(it["@t"]);

                        return <Text style={style}
                        >{logTime.toLocaleString()}:{it["@m"]}:{it["@x"]}</Text>;
                    }}
                />
            </View>
            

        </View>;
    
    }catch(err){
        return <Text style={[siteStyles.danger,siteStyles.centered]}>
            {err||'no data loaded'}
        </Text>;
    }


}

const styles = StyleSheet.create({
    
    statusBlock: {
      flex: 1, 
      flexDirection: 'row',
      flexWrap: 'wrap',
      alignItems: 'flex-start' // if you want to fill rows left to right
    },
    itemTwocolumn: {
      width: '50%', // is 50% of container width
      padding:5
    }
  });