import { useState, useEffect, useRef } from "react";


export function usePrevious<T>(value:T) {
  // The ref object is a generic container whose current property is mutable ...
  // ... and can hold any value, similar to an instance property on a class
  const ref = useRef<T>();
  
  // Store current value in ref
  useEffect(() => {
    ref.current = value;
  }, [value]); // Only re-run if value changes
  
  // Return previous value (happens before update in useEffect above)
  return ref.current;
}

export function useDataFetching<T>(dataSource:string, defaultValue?: T) {
    const [loading, setLoading] = useState(true);
    const [results, setResults] = useState(defaultValue);
    const [error, setError] = useState("");

    useEffect(() => {
        async function fetchData() {
          try {
            setLoading(true);
            const data = await fetch(dataSource);
            const {status, statusText} = data;
            
            if(200 != status){
              let error ='';
              try{ 
                error= (await data.text());
                const errObject = JSON.parse(error);
                if(errObject && errObject.message )
                  error = errObject.message;
              }
              catch{}
              error = error||(statusText||'unknown error');
              throw error;
            }
           
            const json = await data.json() as T;
    
            if (json) {
              setLoading(false);
              setResults(json);
            }
          } catch (error) {
            setLoading(false);
            setError(`failed loading data :${error}`);
          }
    
          setLoading(false);
        }
    
        fetchData();
      }, [dataSource]);
  
    return {
      loading,
      results,
      error
    };
}

